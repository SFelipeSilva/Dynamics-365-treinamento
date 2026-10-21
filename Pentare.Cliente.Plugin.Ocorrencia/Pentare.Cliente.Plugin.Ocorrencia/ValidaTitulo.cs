
using Microsoft.Xrm.Sdk;
using System;

namespace Pentare.Cliente.Plugin.Ocorrencia
{
    public class ValidaTitulo : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            ITracingService tracingService = (ITracingService)
                serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (context.MessageName.Equals("Create", StringComparison.OrdinalIgnoreCase))
                {

                    Entity targetEntity = (Entity)context.InputParameters["Target"];

                    string titulo = targetEntity.GetAttributeValue<string>("title");

                    if (!string.IsNullOrEmpty(titulo) && !string.IsNullOrWhiteSpace(titulo))
                    {
                        string valorNormalizado = titulo.Trim();
                        targetEntity["title"] = valorNormalizado;

                    }
                    else
                    {
                        throw new InvalidPluginExecutionException(
                        "Erro de validação: O título da ocorrência é obrigatório e não pode conter apenas espaços. " +
                        "Por favor, preencha um título para a ocorrência");
                    }
                    
                    targetEntity["ptr_origemcadastro"] = "Plugin Dynamics 365";

                    tracingService.Trace("Campo 'ptr_origemcadastro' preenchido com sucesso.");

                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Erro capturado: {ex}");
                throw new InvalidPluginExecutionException($"Erro ao processar ocorrência.", ex);
            }
        }
    }
}
