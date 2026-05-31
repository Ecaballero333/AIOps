using InstrumentationInterface;
using Microsoft.AspNetCore.Mvc;
using PharmaGo.PharmacyService.IBusinessLogic;
using PharmaGo.PharmacyService.Enums;
using PharmaGo.PharmacyService.Filters;
using PharmaGo.PharmacyService.Models.In.Exports;

namespace PharmaGo.PharmacyService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [TypeFilter(typeof(ExceptionFilter))]
    [AuthorizationFilter(new string[] { nameof(RoleType.Employee) })]
    public class ExportController : Controller
    {
        private readonly IExportManager _exportManager;
        private readonly IStructuredLogger _structuredLogger;

        public ExportController(IExportManager transactionManager, IStructuredLogger structuredLogger)
        {
            _exportManager = transactionManager;
            _structuredLogger = structuredLogger;
        }

        [HttpGet("exporters")]
        public IActionResult GetAllExporters()
        {
            try
            {
                var exporters = _exportManager.GetAllExporters();
                _structuredLogger.LogInformation(
                    "Exporters retrieved",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "exporter_list",
                        ["component"] = "ExportController",
                        ["operation"] = "list_exporters",
                        ["outcome"] = "success"
                    });
                return Ok(exporters);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Exporters retrieval failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "exporter_list_fail",
                        ["component"] = "ExportController",
                        ["operation"] = "list_exporters",
                        ["outcome"] = "failed",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpGet("parameters")]
        public IActionResult GetParameters([FromQuery] string exporterName)
        {
            try
            {
                var parameters = _exportManager.GetParameters(exporterName);
                _structuredLogger.LogInformation(
                    "Exporter parameters retrieved",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "exporter_parameters",
                        ["component"] = "ExportController",
                        ["operation"] = "get_exporter_parameters",
                        ["outcome"] = "success",
                        ["exporter_name"] = exporterName ?? ""
                    });
                return Ok(parameters);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Exporter parameters retrieval failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "exporter_parameters_fail",
                        ["component"] = "ExportController",
                        ["operation"] = "get_exporter_parameters",
                        ["outcome"] = "failed",
                        ["exporter_name"] = exporterName ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpPost]
        public IActionResult ExportDrugs([FromBody] DrugsExportationModel drugExportationModel)
        {
            try
            {
                string token = HttpContext.Request.Headers["Authorization"];
                _exportManager.ExportDrugs(drugExportationModel.FormatName, drugExportationModel.Parameters, token);
                _structuredLogger.LogInformation(
                    "Drug export completed",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_export",
                        ["component"] = "ExportController",
                        ["operation"] = "export_drugs",
                        ["db_operation"] = "read_drugs_for_export",
                        ["outcome"] = "success",
                        ["export_format"] = drugExportationModel.FormatName,
                        ["parameters_count"] = drugExportationModel.Parameters?.Count() ?? 0
                    });
                return Ok(true);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Drug export failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_export_fail",
                        ["component"] = "ExportController",
                        ["operation"] = "export_drugs",
                        ["db_operation"] = "read_drugs_for_export",
                        ["outcome"] = "failed",
                        ["export_format"] = drugExportationModel?.FormatName ?? "",
                        ["parameters_count"] = drugExportationModel?.Parameters?.Count() ?? 0,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }
    }
}
