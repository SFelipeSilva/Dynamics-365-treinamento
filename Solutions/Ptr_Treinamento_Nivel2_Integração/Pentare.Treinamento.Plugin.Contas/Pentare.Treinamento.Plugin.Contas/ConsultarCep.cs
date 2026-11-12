
using Microsoft.Xrm.Sdk;
using System;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Pentare.Treinamento.Plugin.Contas
{
    /// <summary>
    /// Plugin responsável por executar a Custom API ptr_ConsultarCep.
    /// Realiza a consulta de um CEP no serviço externo ViaCEP e disponibiliza
    /// os dados retornados por meio dos parâmetros de saída da Custom API.
    /// </summary>
    public class ConsultarCep : IPlugin
    {
        /// <summary>
        /// Método principal de execução do plugin.
        /// Obtém o contexto da execução, valida a mensagem recebida e inicia
        /// o processo de consulta do endereço.
        /// </summary>
        /// <param name="serviceProvider">
        /// Provedor de serviços disponibilizado pelo Dataverse,
        /// utilizado para obter o contexto da execução e o serviço de rastreamento.
        /// </param>
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext executionContext = (IPluginExecutionContext)serviceProvider.GetService(
                typeof(IPluginExecutionContext));

            try
            {
                tracingService.Trace("Iniciando execução da Custom API ConsultarCep.");

                if (!string.Equals(executionContext.MessageName, "ptr_ConsultarCep", StringComparison.OrdinalIgnoreCase))
                {
                    tracingService.Trace("Mensagem de execução diferente da esperada: {0}", executionContext.MessageName);
                    return;
                }

                string cepInformado = executionContext.InputParameters.Contains("ptr_CEP")
                        ? executionContext.InputParameters["ptr_CEP"] as string : null;

                tracingService.Trace("Parâmetro de entrada recebido.");

                ConsultaEndereco(cepInformado, executionContext, tracingService);

                tracingService.Trace("Execução da Custom API finalizada.");
            }
            catch (Exception ex)
            {
                tracingService.Trace("Falha inesperada na Custom API. Tipo: {0}. Mensagem: {1}", ex.GetType().FullName, ex.Message);

                throw new InvalidPluginExecutionException("Ocorreu um erro inesperado ao consultar o CEP. Tente novamente.", ex);
            }
        }

        /// <summary>
        /// Encaminha a execução para o método assíncrono de consulta,
        /// aguardando sua conclusão de forma síncrona.
        /// </summary>
        /// <param name="cepInformado">CEP recebido como parâmetro de entrada da Custom API.</param>
        /// <param name="context">Contexto de execução do plugin no Dataverse.</param>
        /// <param name="tracing">Serviço utilizado para registrar informações de rastreamento.</param>
        private void ConsultaEndereco(string cepInformado, IPluginExecutionContext context, ITracingService tracing)
        {
            ConsultaEnderecoAsync(cepInformado, context, tracing).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Consulta o serviço ViaCEP utilizando o CEP informado e processa
        /// os dados retornados para preencher os parâmetros de saída da Custom API.
        /// </summary>
        /// <param name="cepInformado">CEP recebido da Custom API, que será normalizado antes da consulta.</param>
        /// <param name="context">Contexto de execução utilizado para definir os parâmetros de saída.</param>
        /// <param name="tracing">Serviço de rastreamento utilizado para registrar o andamento da consulta.</param>
        /// <returns>
        /// Uma tarefa que representa a execução assíncrona da consulta ao ViaCEP.
        /// </returns>
        private async Task ConsultaEnderecoAsync(string cepInformado, IPluginExecutionContext context, ITracingService tracing)
        {
            string cep = Regex.Replace(cepInformado ?? "", @"\D", "");

            DefinirSaida(context, false, cep, "", "", "", "", "CEP inválido. Informe um CEP com 8 dígitos.");

            if (cep.Length != 8)
            {
                tracing.Trace("CEP inválido. Quantidade de dígitos: {0}", cep.Length);
                return;
            }

            string url = $"https://viacep.com.br/ws/{cep}/json/";

            tracing.Trace("Iniciando consulta ao serviço ViaCEP.");

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.ConnectionClose = true;
                    httpClient.Timeout = TimeSpan.FromSeconds(15);

                    HttpResponseMessage response = await httpClient.GetAsync(url).ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(EnderecoViaCep));

                        EnderecoViaCep endereco = (EnderecoViaCep)serializer.ReadObject(stream);

                        if (endereco == null || endereco.Erro)
                        {
                            DefinirSaida(context, false, cep, "", "", "", "", "CEP não encontrado na base do ViaCEP.");

                            tracing.Trace("CEP não encontrado na base do ViaCEP.");

                            return;
                        }

                        DefinirSaida(context, true, endereco.Cep ?? cep, endereco.Logradouro ?? "", endereco.Bairro ?? "",
                            endereco.Localidade ?? "", endereco.Uf ?? "", "CEP consultado com sucesso.");

                        tracing.Trace("Consulta ao ViaCEP concluída com sucesso.");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                tracing.Trace("Tempo limite excedido ao aguardar resposta do ViaCEP.");

                DefinirSaida(context, false, cep, "", "", "", "", "Tempo limite ao consultar o CEP. Tente novamente.");
            }
            catch (HttpRequestException httpEx)
            {
                tracing.Trace("Falha HTTP ou de comunicação com ViaCEP. Tipo: {0}. Status: {1}", httpEx.GetType().FullName, httpEx.Message);

                DefinirSaida(context, false, cep, "", "", "", "", "Não foi possível consultar o serviço ViaCEP. Tente novamente.");
            }
            catch (Exception ex)
            {
                tracing.Trace("Erro ao processar a resposta do ViaCEP. Tipo: {0}. Mensagem: {1}",ex.GetType().FullName,ex.Message);

                DefinirSaida(context, false, cep, "", "", "", "", "Ocorreu um erro ao processar o endereço retornado.");
            }
        }

        /// <summary>
        /// Define os parâmetros de saída da Custom API com o resultado da consulta.
        /// </summary>
        /// <param name="context">Contexto de execução da Custom API.</param>
        /// <param name="sucesso">Indica se a consulta ao ViaCEP foi concluída com sucesso.</param>
        /// <param name="cep">CEP consultado ou normalizado.</param>
        /// <param name="logradouro">Nome da rua, avenida ou logradouro retornado pelo serviço.</param>
        /// <param name="bairro">Bairro correspondente ao CEP.</param>
        /// <param name="cidade">Cidade correspondente ao CEP.</param>
        /// <param name="uf">Sigla da unidade federativa.</param>
        /// <param name="mensagem">Mensagem descritiva sobre o resultado da consulta.</param>
        private static void DefinirSaida(IPluginExecutionContext context, bool sucesso, string cep, string logradouro,
            string bairro, string cidade, string uf, string mensagem)
        {
            context.OutputParameters["ptr_Sucesso"] = sucesso;
            context.OutputParameters["ptr_Cep"] = cep;
            context.OutputParameters["ptr_Logradouro"] = logradouro;
            context.OutputParameters["ptr_Bairro"] = bairro;
            context.OutputParameters["ptr_Cidade"] = cidade;
            context.OutputParameters["ptr_UF"] = uf;
            context.OutputParameters["ptr_Mensagem"] = mensagem;
        }

        /// <summary>
        /// Representa a estrutura JSON retornada pelo serviço ViaCEP.
        /// Os atributos DataMember associam as propriedades C# aos nomes
        /// correspondentes presentes na resposta JSON.
        /// </summary>
        [DataContract]
        private class EnderecoViaCep
        {
            [DataMember(Name = "cep")]
            public string Cep { get; set; }

            [DataMember(Name = "logradouro")]
            public string Logradouro { get; set; }

            [DataMember(Name = "bairro")]
            public string Bairro { get; set; }

            [DataMember(Name = "localidade")]
            public string Localidade { get; set; }

            [DataMember(Name = "uf")]
            public string Uf { get; set; }

            [DataMember(Name = "erro")]
            public bool Erro { get; set; }
        }
    }
}
