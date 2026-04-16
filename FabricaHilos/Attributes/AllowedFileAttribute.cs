using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FabricaHilos.Attributes
{
    /// <summary>
    /// Valida que un <see cref="IFormFile"/> cumpla con los tipos MIME permitidos y el tamaño máximo.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public sealed class AllowedFileAttribute : ValidationAttribute
    {
        private readonly string[] _allowedContentTypes;
        private readonly long _maxBytes;

        /// <param name="maxMegabytes">Tamaño máximo en MB.</param>
        /// <param name="allowedContentTypes">Tipos MIME permitidos, p. ej. "image/jpeg".</param>
        public AllowedFileAttribute(double maxMegabytes, params string[] allowedContentTypes)
        {
            _maxBytes = (long)(maxMegabytes * 1024 * 1024);
            _allowedContentTypes = allowedContentTypes;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not IFormFile file)
                return ValidationResult.Success; // [Required] maneja el caso nulo

            if (file.Length > _maxBytes)
            {
                double mb = _maxBytes / 1024.0 / 1024.0;
                return new ValidationResult($"El archivo no puede superar los {mb:0.#} MB.");
            }

            if (_allowedContentTypes.Length > 0 &&
                !_allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                var allowed = string.Join(", ", _allowedContentTypes);
                return new ValidationResult($"Tipo de archivo no permitido. Solo se aceptan: {allowed}.");
            }

            return ValidationResult.Success;
        }
    }
}
