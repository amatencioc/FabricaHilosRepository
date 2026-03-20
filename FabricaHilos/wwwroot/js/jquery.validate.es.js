// Localización en español para jQuery Validation
// Se carga después de jquery.validate para sobrescribir todos los mensajes por defecto.
(function ($) {
    $.extend($.validator.messages, {
        required:     "Este campo es obligatorio.",
        remote:       "Por favor, corrija este campo.",
        email:        "Por favor, ingrese un correo electrónico válido.",
        url:          "Por favor, ingrese una URL válida.",
        date:         "Por favor, ingrese una fecha válida.",
        dateISO:      "Por favor, ingrese una fecha válida (formato ISO).",
        number:       "Por favor, ingrese un número válido.",
        digits:       "Por favor, ingrese sólo dígitos.",
        creditcard:   "Por favor, ingrese un número de tarjeta de crédito válido.",
        equalTo:      "Por favor, ingrese el mismo valor nuevamente.",
        maxlength:    $.validator.format("Por favor, no ingrese más de {0} caracteres."),
        minlength:    $.validator.format("Por favor, ingrese al menos {0} caracteres."),
        rangelength:  $.validator.format("Por favor, ingrese un valor entre {0} y {1} caracteres."),
        range:        $.validator.format("Por favor, ingrese un valor entre {0} y {1}."),
        max:          $.validator.format("Por favor, ingrese un valor menor o igual a {0}."),
        min:          $.validator.format("Por favor, ingrese un valor mayor o igual a {0}."),
        step:         $.validator.format("Por favor, ingrese un múltiplo de {0}.")
    });
}(jQuery));
