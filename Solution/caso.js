if (typeof (Pentare) == "undefined") { Pentare = {}; }

Pentare.Caso = {
    formContext: {},

    OnLoad: function (executionContext) {
        Pentare.Caso.formContext = executionContext.getFormContext();
        Pentare.Caso.OnChange();
        Pentare.Caso.BuscaCliente();
    },

    OnChange: function () {
        Pentare.Caso.formContext.getAttribute("customerid").addOnChange(Pentare.Caso.BuscaCliente);
    },

    BuscaCliente: async function () {
        var formContext = Pentare.Caso.formContext;
        var atributoCliente = formContext.getAttribute("customerid");
        var atributoTelefone = formContext.getAttribute("ptr_telefonecliente");
        var atributoEmail = formContext.getAttribute("ptr_emailcliente");
        var cliente = atributoCliente.getValue();

        if (!cliente || cliente.length === 0) {
            atributoTelefone.setValue(null);
            atributoEmail.setValue(null);
            return;
        }

        var customerItem = cliente[0];
        var tipoCliente = customerItem.entityType;

        if (tipoCliente !== "account" && tipoCliente !== "contact") {
            atributoTelefone.setValue(null);
            atributoEmail.setValue(null);
            return;
        }

        var recordId = customerItem.id.replace(/[{}]/g, "");

        atributoTelefone.setValue(null);
        atributoEmail.setValue(null);

        try {
            var resultado = await Xrm.WebApi.retrieveRecord(tipoCliente, recordId, "?$select=telephone1,emailaddress1");
            var clienteAtual = atributoCliente.getValue();

            if (!clienteAtual || clienteAtual.length === 0 || clienteAtual[0].id.replace(/[{}]/g, "") !== recordId) {
                return;
            }

            atributoTelefone.setValue(resultado.telephone1 || null);
            atributoEmail.setValue(resultado.emailaddress1 || null);

        } catch (error) {
            console.log("Erro ao buscar dados do cliente: " + error.message);
            atributoTelefone.setValue(null);
            atributoEmail.setValue(null);
        }
    }
};