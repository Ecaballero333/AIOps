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
    public class DrugController : Controller
    {
        private readonly IDrugManager _drugManager;
        private readonly IStructuredLogger _structuredLogger;

        public DrugController(IDrugManager manager, IStructuredLogger structuredLogger)
        {
            _drugManager = manager;
            _structuredLogger = structuredLogger;
        }

        [HttpGet]
        public IActionResult GetAll([FromQuery] DrugSearchCriteria drugSearchCriteria)
        {
            IEnumerable<Drug> drugs = _drugManager.GetAll(drugSearchCriteria);
            IEnumerable<DrugBasicModel> drugsToReturn = drugs.Select(d => new DrugBasicModel(d));
            return Ok(drugsToReturn);
        }

        [HttpGet]
        [Route("[action]")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Employee) })]
        public IActionResult User()
        {
            string token = HttpContext.Request.Headers["Authorization"];
            IEnumerable<Drug> drugs = _drugManager.GetAllByUser(token);
            IEnumerable<DrugBasicModel> drugsToReturn = drugs.Select(d => new DrugBasicModel(d));
            return Ok(drugsToReturn);
        }

        [HttpGet("{id}")]

        public IActionResult GetById([FromRoute] int id)
        {
            Drug drug = _drugManager.GetById(id);
            return Ok(new DrugDetailModel(drug));
        }

        [HttpPost]
        [AuthorizationFilter(new string[] { nameof(RoleType.Employee) })]
        public IActionResult Create([FromBody] DrugModel drugModel)
        {
            string token = HttpContext.Request.Headers["Authorization"];
            Drug drugCreated = _drugManager.Create(drugModel.ToEntity(), token);
            DrugDetailModel drugResponse = new DrugDetailModel(drugCreated);
            return Ok(drugResponse);
        }

        [HttpPut("{id}")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Administrator) })]
        public IActionResult Update([FromRoute] int id, [FromBody] UpdateDrugModel updatedDrug)
        {
            try
            {
                Drug drug = _drugManager.Update(id, updatedDrug.ToEntity());
                _structuredLogger.LogInformation(
                    "Drug updated",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_update",
                        ["component"] = "DrugController",
                        ["operation"] = "update_drug",
                        ["db_operation"] = "update_drug",
                        ["outcome"] = "success",
                        ["drug_id"] = drug.Id,
                        ["drug_name"] = drug.Name
                    });
                return Ok(new DrugDetailModel(drug));
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Drug update failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_update_fail",
                        ["component"] = "DrugController",
                        ["operation"] = "update_drug",
                        ["db_operation"] = "update_drug",
                        ["outcome"] = "failed",
                        ["drug_id"] = id,
                        ["drug_name"] = updatedDrug?.Name ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpDelete("{id}")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Employee) })]
        public IActionResult Delete([FromRoute] int id)
        {
            try
            {
                _drugManager.Delete(id);
                _structuredLogger.LogInformation(
                    "Drug deleted",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_delete",
                        ["component"] = "DrugController",
                        ["operation"] = "delete_drug",
                        ["db_operation"] = "delete_drug",
                        ["outcome"] = "success",
                        ["drug_id"] = id
                    });
                return Ok(true);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Drug deletion failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_delete_fail",
                        ["component"] = "DrugController",
                        ["operation"] = "delete_drug",
                        ["db_operation"] = "delete_drug",
                        ["outcome"] = "failed",
                        ["drug_id"] = id,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }
    }
}
