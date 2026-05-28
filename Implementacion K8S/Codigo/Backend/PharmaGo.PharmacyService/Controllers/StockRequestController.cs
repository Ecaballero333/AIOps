using InstrumentationInterface;
using Microsoft.AspNetCore.Mvc;
using PharmaGo.Domain.Entities;
using PharmaGo.Exceptions;
using PharmaGo.PharmacyService.IBusinessLogic;
using PharmaGo.PharmacyService.Enums;
using PharmaGo.PharmacyService.Filters;
using PharmaGo.PharmacyService.Models.In;
using PharmaGo.PharmacyService.Models.Out;

namespace PharmaGo.PharmacyService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StockRequestController : ControllerBase
    {
        private readonly IStockRequestManager _stockRequestManager;
        private readonly IStructuredLogger _structuredLogger;

        public StockRequestController(IStockRequestManager manager, IStructuredLogger structuredLogger)
        {
            _stockRequestManager = manager;
            _structuredLogger = structuredLogger;
        }

        [HttpPost]
        [AuthorizationFilter(new string[] { nameof(RoleType.Employee) })]
        public IActionResult CreateStockRequest([FromBody] StockRequestModelRequest stockRequestModel)
        {
            try
            {
                string token = HttpContext.Request.Headers["Authorization"];
                var stockRequest = _stockRequestManager.CreateStockRequest(stockRequestModel.ToEntity(), token);
                _structuredLogger.LogInformation(
                    "Stock request created",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_create",
                        ["component"] = "StockRequestController",
                        ["operation"] = "create_stock_request",
                        ["db_operation"] = "insert_stock_request",
                        ["outcome"] = "success",
                        ["stock_request_id"] = stockRequest.Id,
                        ["details_count"] = stockRequest.Details?.Count ?? 0,
                        ["status"] = stockRequest.Status.ToString()
                    });
                return Ok(new StockRequestModelResponse(stockRequest));
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Stock request creation failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_create_fail",
                        ["component"] = "StockRequestController",
                        ["operation"] = "create_stock_request",
                        ["db_operation"] = "insert_stock_request",
                        ["outcome"] = "failed",
                        ["details_count"] = stockRequestModel?.Details?.Count ?? 0,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpPut]
        [AuthorizationFilter(new string[] { nameof(RoleType.Owner) })]
        [Route("[action]/{id}")]
        public IActionResult ApproveStockRequest(int id)
        {
            try
            {
                var approved = _stockRequestManager.ApproveStockRequest(id);
                _structuredLogger.LogInformation(
                    "Stock request approved",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_approve",
                        ["component"] = "StockRequestController",
                        ["operation"] = "approve_stock_request",
                        ["db_operation"] = "update_stock_request_and_drug_stock",
                        ["outcome"] = "success",
                        ["stock_request_id"] = id,
                        ["approved"] = approved
                    });
                return Ok(approved);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Stock request approval failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_approve_fail",
                        ["component"] = "StockRequestController",
                        ["operation"] = "approve_stock_request",
                        ["db_operation"] = "update_stock_request_and_drug_stock",
                        ["outcome"] = "failed",
                        ["stock_request_id"] = id,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpPut]
        [Route("[action]/{id}")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Owner) })]
        public IActionResult RejectStockRequest(int id)
        {
            try
            {
                var rejected = _stockRequestManager.RejectStockRequest(id);
                _structuredLogger.LogInformation(
                    "Stock request rejected",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_reject",
                        ["component"] = "StockRequestController",
                        ["operation"] = "reject_stock_request",
                        ["db_operation"] = "update_stock_request",
                        ["outcome"] = "success",
                        ["stock_request_id"] = id,
                        ["rejected"] = rejected
                    });
                return Ok(rejected);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Stock request rejection failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_reject_fail",
                        ["component"] = "StockRequestController",
                        ["operation"] = "reject_stock_request",
                        ["db_operation"] = "update_stock_request",
                        ["outcome"] = "failed",
                        ["stock_request_id"] = id,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpGet]
        [Route("[action]")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Employee) })]
        public IActionResult ByEmployee([FromQuery] StockRequestSearchCriteriaModelRequest searchCriteria)
        {
            try
            {
                string token = HttpContext.Request.Headers["Authorization"];
                var stockRequests = _stockRequestManager.GetStockRequestsByEmployee(token, searchCriteria.ToEntity());

                var result = stockRequests.Select(d => new StockRequestSearchCriteriaModelResponse(d)).ToList();
                _structuredLogger.LogInformation(
                    "Stock requests retrieved by employee",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_list_by_employee",
                        ["component"] = "StockRequestController",
                        ["operation"] = "list_stock_requests_by_employee",
                        ["db_operation"] = "read_stock_requests",
                        ["outcome"] = "success",
                        ["stock_requests_count"] = result.Count,
                        ["from_date"] = searchCriteria?.FromDate?.ToString("O") ?? "",
                        ["to_date"] = searchCriteria?.ToDate?.ToString("O") ?? "",
                        ["drug_code"] = searchCriteria?.Code ?? "",
                        ["status"] = searchCriteria?.Status ?? ""
                    });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Stock requests retrieval by employee failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_list_by_employee_fail",
                        ["component"] = "StockRequestController",
                        ["operation"] = "list_stock_requests_by_employee",
                        ["db_operation"] = "read_stock_requests",
                        ["outcome"] = "failed",
                        ["from_date"] = searchCriteria?.FromDate?.ToString("O") ?? "",
                        ["to_date"] = searchCriteria?.ToDate?.ToString("O") ?? "",
                        ["drug_code"] = searchCriteria?.Code ?? "",
                        ["status"] = searchCriteria?.Status ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpGet]
        [AuthorizationFilter(new string[] { nameof(RoleType.Owner) })]
        public IActionResult GetAll()
        {
            try
            {
                string token = HttpContext.Request.Headers["Authorization"];
                var stockRequests = _stockRequestManager.GetStockRequestsByOwner(token);

                var result = stockRequests.Select(d => new StockRequestSearchCriteriaModelResponse(d)).ToList();
                _structuredLogger.LogInformation(
                    "Stock requests retrieved by owner",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_list_by_owner",
                        ["component"] = "StockRequestController",
                        ["operation"] = "list_stock_requests_by_owner",
                        ["db_operation"] = "read_stock_requests",
                        ["outcome"] = "success",
                        ["stock_requests_count"] = result.Count
                    });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Stock requests retrieval by owner failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_list_by_owner_fail",
                        ["component"] = "StockRequestController",
                        ["operation"] = "list_stock_requests_by_owner",
                        ["db_operation"] = "read_stock_requests",
                        ["outcome"] = "failed",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }
    }
}
