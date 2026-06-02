using InstrumentationInterface;
using Microsoft.AspNetCore.Mvc;
using PharmaGo.Domain.Entities;
using PharmaGo.Domain.SearchCriterias;
using PharmaGo.PharmacyService.IBusinessLogic;
using PharmaGo.PharmacyService.Enums;
using PharmaGo.PharmacyService.Filters;
using PharmaGo.PharmacyService.Models.In;
using PharmaGo.PharmacyService.Models.Out;

namespace PharmaGo.PharmacyService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [TypeFilter(typeof(ExceptionFilter))]
    public class PharmacyController : Controller
    {
        private readonly IPharmacyManager _pharmacyManager;
        private readonly IStructuredLogger _structuredLogger;

        public PharmacyController(IPharmacyManager pharmacyManager, IStructuredLogger structuredLogger)
        {
            _pharmacyManager = pharmacyManager;
            _structuredLogger = structuredLogger;
        }

        [HttpGet]
        public IActionResult GetAll([FromQuery] PharmacySearchCriteria pharmacySearchCriteria)
        {
            try
            {
                IEnumerable<Pharmacy> pharmacies = _pharmacyManager.GetAll(pharmacySearchCriteria);
                List<PharmacyBasicModel> pharmaciesToReturn = pharmacies.Select(p => new PharmacyBasicModel(p)).ToList();
                // _structuredLogger.LogInformation(
                //     "Pharmacies retrieved",
                //     new Dictionary<string, object>
                //     {
                //         ["pharma_biz"] = "pharmacy_list",
                //         ["component"] = "PharmacyController",
                //         ["operation"] = "list_pharmacies",
                //         ["db_operation"] = "read_pharmacies",
                //         ["outcome"] = "success",
                //         ["pharmacies_count"] = pharmaciesToReturn.Count,
                //         ["pharmacy_name"] = pharmacySearchCriteria?.Name ?? "",
                //         ["pharmacy_address"] = pharmacySearchCriteria?.Address ?? ""
                //     });
                return Ok(pharmaciesToReturn);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Pharmacies retrieval failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_list_fail",
                        ["component"] = "PharmacyController",
                        ["operation"] = "list_pharmacies",
                        ["db_operation"] = "read_pharmacies",
                        ["outcome"] = "failed",
                        ["pharmacy_name"] = pharmacySearchCriteria?.Name ?? "",
                        ["pharmacy_address"] = pharmacySearchCriteria?.Address ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpGet("{id}")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Administrator) })]
        public IActionResult GetById([FromRoute] int id)
        {
            try
            {
                Pharmacy pharmacy = _pharmacyManager.GetById(id);
                _structuredLogger.LogInformation(
                    "Pharmacy retrieved by id",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_get_by_id",
                        ["component"] = "PharmacyController",
                        ["operation"] = "get_pharmacy_by_id",
                        ["db_operation"] = "read_pharmacy",
                        ["outcome"] = "success",
                        ["pharmacy_id"] = pharmacy.Id,
                        ["pharmacy_name"] = pharmacy.Name
                    });
                return Ok(new PharmacyDetailModel(pharmacy));
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Pharmacy retrieval by id failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_get_by_id_fail",
                        ["component"] = "PharmacyController",
                        ["operation"] = "get_pharmacy_by_id",
                        ["db_operation"] = "read_pharmacy",
                        ["outcome"] = "failed",
                        ["pharmacy_id"] = id,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpPost]
        [AuthorizationFilter(new string[] { nameof(RoleType.Administrator) })]
        public IActionResult Create([FromBody] PharmacyModel pharmacyModel)
        {
            try
            {
                Pharmacy pharmacyCreated = _pharmacyManager.Create(pharmacyModel.ToEntity());
                PharmacyDetailModel pharmacyResponse = new PharmacyDetailModel(pharmacyCreated);
                _structuredLogger.LogInformation(
                    "Pharmacy created",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_create",
                        ["component"] = "PharmacyController",
                        ["operation"] = "create_pharmacy",
                        ["db_operation"] = "insert_pharmacy",
                        ["outcome"] = "success",
                        ["pharmacy_id"] = pharmacyCreated.Id,
                        ["pharmacy_name"] = pharmacyCreated.Name
                    });
                return Ok(pharmacyResponse);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Pharmacy creation failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_create_fail",
                        ["component"] = "PharmacyController",
                        ["operation"] = "create_pharmacy",
                        ["db_operation"] = "insert_pharmacy",
                        ["outcome"] = "failed",
                        ["pharmacy_name"] = pharmacyModel?.Name ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpPut("{id}")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Administrator) })]
        public IActionResult Update([FromRoute] int id, [FromBody] PharmacyModel updatedPharmacy)
        {
            try
            {
                Pharmacy pharmacy = _pharmacyManager.Update(id, updatedPharmacy.ToEntity());
                _structuredLogger.LogInformation(
                    "Pharmacy updated",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_update",
                        ["component"] = "PharmacyController",
                        ["operation"] = "update_pharmacy",
                        ["db_operation"] = "update_pharmacy",
                        ["outcome"] = "success",
                        ["pharmacy_id"] = pharmacy.Id,
                        ["pharmacy_name"] = pharmacy.Name
                    });
                return Ok(new PharmacyDetailModel(pharmacy));
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Pharmacy update failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_update_fail",
                        ["component"] = "PharmacyController",
                        ["operation"] = "update_pharmacy",
                        ["db_operation"] = "update_pharmacy",
                        ["outcome"] = "failed",
                        ["pharmacy_id"] = id,
                        ["pharmacy_name"] = updatedPharmacy?.Name ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpDelete("{id}")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Administrator) })]
        public IActionResult Delete([FromRoute] int id)
        {
            try
            {
                _pharmacyManager.Delete(id);
                _structuredLogger.LogInformation(
                    "Pharmacy deleted",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_delete",
                        ["component"] = "PharmacyController",
                        ["operation"] = "delete_pharmacy",
                        ["db_operation"] = "delete_pharmacy",
                        ["outcome"] = "success",
                        ["pharmacy_id"] = id
                    });
                return Ok(true);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Pharmacy deletion failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_delete_fail",
                        ["component"] = "PharmacyController",
                        ["operation"] = "delete_pharmacy",
                        ["db_operation"] = "delete_pharmacy",
                        ["outcome"] = "failed",
                        ["pharmacy_id"] = id,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }
    }
}
