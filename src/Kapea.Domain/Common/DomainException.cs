namespace Kapea.Domain.Common;

/// <summary>
/// Violación de una invariante del dominio. Se distingue de un fallo de validación
/// de entrada: aquí el dato ya ha entrado y el modelo se niega a quedar incoherente.
/// </summary>
public class DomainException(string message) : Exception(message);
