
if (typeof (Pentare) == "undefined") { Pentare = {}; }

Pentare.Contato = {

    formContext: {},

    OnLoad: function (executionContext) {
        Pentare.Contato.formContext = executionContext.getFormContext();
        Pentare.Contato.OnChange();
    },

    OnChange: function (){
        Pentare.Contato.formContext.getAttribute("telephone1").addOnChange(Pentare.Contato.ValidarTelefoneOnChange);
        Pentare.Contato.formContext.getAttribute("emailaddress1").addOnChange(Pentare.Contato.ValidarEmailOnChange);
    },

    OnSave: function (executionContext){
        Pentare.Contato.ValidarTelefoneOnSave(executionContext);
        Pentare.Contato.ValidarEmailOnSave(executionContext);
    },

    ValidarTelefoneOnChange: function () {
        var formContext = Pentare.Contato.formContext;
        var valor = formContext.getAttribute("telephone1").getValue();
        var telefoneControl = formContext.getControl("telephone1");

        if (!valor) {
            telefoneControl.clearNotification("telefone_invalido");
            return;
        }

        var telefone = valor.replace(/\D/g, "");

        if (telefone.length !== 10 && telefone.length !== 11) {
            telefoneControl.setNotification(
                "Telefone inválido.",
                "telefone_invalido"
            );
        } else {
            telefoneControl.clearNotification("telefone_invalido");
        }
    },

    ValidarEmailOnChange: function () {
        var formContext = Pentare.Contato.formContext;
        var email = formContext.getAttribute("emailaddress1").getValue();
        var emailControl = formContext.getControl("emailaddress1");

        if (!email) {
            emailControl.clearNotification("email_invalido");
            return;
        }
        var regexEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

        if (!regexEmail.test(email)) {
            emailControl.setNotification(
                "E-mail inválido.",
                "email_invalido"
            );
            
        } else {
            emailControl.clearNotification("email_invalido");
        }

        var dominio = email.substring(email.lastIndexOf("@") + 1);

        if (!dominio.toLowerCase() === "teste.com"){
            emailControl.clearNotification("dominio_bloqueado");
        }
    },

    ValidarTelefoneOnSave: function (executionContext) {
        var formContext = executionContext.getFormContext();
        var valor = formContext.getAttribute("telephone1").getValue();
        var telefoneControl = formContext.getControl("telephone1");

        if (!valor) return;

        var telefone = valor.replace(/\D/g, "");

        if (telefone.length !== 10 && telefone.length !== 11) {
            executionContext.getEventArgs().preventDefault();

            telefoneControl.setNotification(
                "Telefone inválido. Corrija o campo antes de salvar.",
                "telefone_invalido"
            );
            telefoneControl.setFocus();
        } else {
            telefoneControl.clearNotification("telefone_invalido");
        }
    },

    ValidarEmailOnSave: function (executionContext) {
        var formContext = executionContext.getFormContext();
        var email = formContext.getAttribute("emailaddress1").getValue();
        var emailControl = formContext.getControl("emailaddress1");

        if (!email) return;

        email = email.trim();

        var regexEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

        if (!regexEmail.test(email)) {
             executionContext.getEventArgs().preventDefault();

            emailControl.setNotification(
                "E-mail inválido. Corrija o campo antes de salvar.",
                "email_invalido"
            );
            emailControl.setFocus();
            
        } else {
            emailControl.clearNotification("email_invalido");
        }

        var dominio = email.substring(email.lastIndexOf("@") + 1);

        if (dominio.toLowerCase() === "teste.com") {
            executionContext.getEventArgs().preventDefault();

            emailControl.setNotification(
                "O domínio teste.com não é permitido. Informe outro e-mail.",
                "dominio_bloqueado"
            );

            emailControl.setFocus();
        } else {
            emailControl.clearNotification("dominio_bloqueado");
        }
    }
}