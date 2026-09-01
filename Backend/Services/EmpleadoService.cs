using System;
using System.Collections.Generic;
using System.Linq;
using Nomina.Backend.Data;
using Nomina.Backend.Models;

namespace Nomina.Backend.Services
{
    /// <summary>
    /// Lógica de negocio y acceso a datos (CRUD) para la entidad Empleado.
    /// Cada método abre su propio DbContext de vida corta (patrón recomendado
    /// para apps de escritorio: evita fugas de memoria y problemas de
    /// concurrencia con SQLite).
    /// </summary>
    public class EmpleadoService
    {
        /// <summary>SMMLV 2026, usado para validar reglas de negocio al crear/editar.</summary>
        private const decimal SMMLV_2026 = 1_750_905m;

        /// <summary>
        /// Crea un nuevo empleado. Calcula automáticamente si tiene derecho
        /// a Auxilio de Transporte (<= 2 SMMLV), aunque el valor puede
        /// ser sobrescrito manualmente desde el Frontend antes de llamar aquí.
        /// </summary>
        public Empleado Crear(Empleado empleado)
        {
            ValidarEmpleado(empleado);

            using var context = new AppDbContext();

            // Evitar documentos duplicados (regla de negocio, además del índice único en BD)
            bool existeDocumento = context.Empleados.Any(e => e.Documento == empleado.Documento);
            if (existeDocumento)
                throw new InvalidOperationException($"Ya existe un empleado con el documento {empleado.Documento}.");

            // Sugerencia automática de auxilio de transporte según ley 2026
            empleado.AuxilioTransporte = empleado.SalarioBasico <= (SMMLV_2026 * 2);

            context.Empleados.Add(empleado);
            context.SaveChanges();

            return empleado;
        }

        /// <summary>Obtiene todos los empleados (opcionalmente filtrando por estado activo).</summary>
        public List<Empleado> ObtenerTodos(bool soloActivos = false)
        {
            using var context = new AppDbContext();

            var query = context.Empleados.AsQueryable();

            if (soloActivos)
                query = query.Where(e => e.Estado == EstadoEmpleado.Activo);

            return query.OrderBy(e => e.Apellido).ThenBy(e => e.Nombre).ToList();
        }

        /// <summary>Obtiene un empleado por su Id. Retorna null si no existe.</summary>
        public Empleado? ObtenerPorId(int id)
        {
            using var context = new AppDbContext();
            return context.Empleados.FirstOrDefault(e => e.Id == id);
        }

        /// <summary>Obtiene un empleado por su número de documento.</summary>
        public Empleado? ObtenerPorDocumento(string documento)
        {
            using var context = new AppDbContext();
            return context.Empleados.FirstOrDefault(e => e.Documento == documento);
        }

        /// <summary>
        /// Actualiza los datos de un empleado existente.
        /// Recalcula el derecho a auxilio de transporte si el salario cambió.
        /// </summary>
        public Empleado Actualizar(Empleado empleadoActualizado)
        {
            ValidarEmpleado(empleadoActualizado);

            using var context = new AppDbContext();

            var empleadoExistente = context.Empleados.FirstOrDefault(e => e.Id == empleadoActualizado.Id)
                ?? throw new InvalidOperationException($"No se encontró el empleado con Id {empleadoActualizado.Id}.");

            // Validar que el documento nuevo no choque con el de otro empleado distinto
            bool documentoEnUso = context.Empleados
                .Any(e => e.Documento == empleadoActualizado.Documento && e.Id != empleadoActualizado.Id);
            if (documentoEnUso)
                throw new InvalidOperationException($"El documento {empleadoActualizado.Documento} ya está en uso por otro empleado.");

            empleadoExistente.Documento = empleadoActualizado.Documento;
            empleadoExistente.Nombre = empleadoActualizado.Nombre;
            empleadoExistente.Apellido = empleadoActualizado.Apellido;
            empleadoExistente.Cargo = empleadoActualizado.Cargo;
            empleadoExistente.SalarioBasico = empleadoActualizado.SalarioBasico;
            empleadoExistente.FechaIngreso = empleadoActualizado.FechaIngreso;
            empleadoExistente.TipoContrato = empleadoActualizado.TipoContrato;
            empleadoExistente.Estado = empleadoActualizado.Estado;

            // Recalcular auxilio de transporte automáticamente según el nuevo salario
            empleadoExistente.AuxilioTransporte = empleadoActualizado.SalarioBasico <= (SMMLV_2026 * 2);

            context.SaveChanges();

            return empleadoExistente;
        }

        /// <summary>
        /// Elimina un empleado de forma definitiva.
        /// ADVERTENCIA: Falla si el empleado tiene nóminas asociadas (por el
        /// DeleteBehavior.Restrict configurado en Database.cs), para proteger
        /// el histórico legal de pagos. Usar CambiarEstado() para "desactivar"
        /// en su lugar cuando ya tiene nóminas generadas.
        /// </summary>
        public void Eliminar(int id)
        {
            using var context = new AppDbContext();

            var empleado = context.Empleados.FirstOrDefault(e => e.Id == id)
                ?? throw new InvalidOperationException($"No se encontró el empleado con Id {id}.");

            try
            {
                context.Empleados.Remove(empleado);
                context.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "No se puede eliminar el empleado porque tiene nóminas asociadas. " +
                    "Cambie su estado a 'Retirado' en su lugar.", ex);
            }
        }

        /// <summary>
        /// Cambia únicamente el estado del empleado (ej. pasar a "Retirado")
        /// sin borrar su historial. Es la forma recomendada de "dar de baja".
        /// </summary>
        public void CambiarEstado(int id, EstadoEmpleado nuevoEstado)
        {
            using var context = new AppDbContext();

            var empleado = context.Empleados.FirstOrDefault(e => e.Id == id)
                ?? throw new InvalidOperationException($"No se encontró el empleado con Id {id}.");

            empleado.Estado = nuevoEstado;
            context.SaveChanges();
        }

        /// <summary>Validaciones básicas de integridad antes de guardar en BD.</summary>
        private void ValidarEmpleado(Empleado empleado)
        {
            if (string.IsNullOrWhiteSpace(empleado.Documento))
                throw new ArgumentException("El documento del empleado es obligatorio.");

            if (string.IsNullOrWhiteSpace(empleado.Nombre) || string.IsNullOrWhiteSpace(empleado.Apellido))
                throw new ArgumentException("El nombre y apellido del empleado son obligatorios.");

            if (empleado.SalarioBasico < SMMLV_2026)
                throw new ArgumentException(
                    $"El salario básico (${empleado.SalarioBasico:N0}) no puede ser inferior al SMMLV 2026 (${SMMLV_2026:N0}).");

            if (empleado.FechaIngreso > DateTime.Now)
                throw new ArgumentException("La fecha de ingreso no puede ser una fecha futura.");
        }
    }
}