using InstrumentationInterface;
using Microsoft.AspNetCore.Mvc;
using PharmaGo.PharmacyService.IBusinessLogic;
using PharmaGo.PharmacyService.Converters;
using PharmaGo.PharmacyService.Enums;
using PharmaGo.PharmacyService.Filters;
using PharmaGo.PharmacyService.Models.In;
using PharmaGo.PharmacyService.Models.Out;

namespace PharmaGo.PharmacyService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchasesController : ControllerBase
    {
        private readonly IPurchasesManager _purchasesManager;
        private readonly IStructuredLogger _structuredLogger;
        private readonly ICustomMetrics _customMetrics;

        public PurchasesController(IPurchasesManager manager, IStructuredLogger structuredLogger, ICustomMetrics customMetrics)
        {
            _purchasesManager = manager;
            _structuredLogger = structuredLogger;
            _customMetrics = customMetrics;
        }

        [HttpGet]
        [AuthorizationFilter(new string[] { nameof(RoleType.Employee) })]
        public IActionResult All()
        {
            try
            {
                string token = HttpContext.Request.Headers["Authorization"];
                var retrievedPuerchases = _purchasesManager.GetAllPurchases(token)
                    .Select(p => new PurchaseModelResponse(p)).ToList();
                _structuredLogger.LogInformation(
                    "Purchases retrieved",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_list",
                        ["component"] = "PurchasesController",
                        ["operation"] = "list_purchases",
                        ["db_operation"] = "read_purchases",
                        ["outcome"] = "success",
                        ["purchases_count"] = retrievedPuerchases.Count
                    });
                return Ok(retrievedPuerchases);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Purchases retrieval failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_list_fail",
                        ["component"] = "PurchasesController",
                        ["operation"] = "list_purchases",
                        ["db_operation"] = "read_purchases",
                        ["outcome"] = "failed",
                        ["error_message"] = ex.Message
                    });
                throw;
            }

        }

        [HttpGet]
        [Route("[action]")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Owner) })]
        public IActionResult ByDate([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            try
            {
                string token = HttpContext.Request.Headers["Authorization"];
                var retrievedPuerchases = _purchasesManager.GetAllPurchasesByDate(token, start, end)
                    .Select(p => new PurchaseModelResponse(p)).ToList();
                _structuredLogger.LogInformation(
                    "Purchases retrieved by date",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_list_by_date",
                        ["component"] = "PurchasesController",
                        ["operation"] = "list_purchases_by_date",
                        ["db_operation"] = "read_purchases_by_date",
                        ["outcome"] = "success",
                        ["purchases_count"] = retrievedPuerchases.Count,
                        ["start_date"] = start?.ToString("O") ?? "",
                        ["end_date"] = end?.ToString("O") ?? ""
                    });
                return Ok(retrievedPuerchases);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Purchases retrieval by date failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_list_by_date_fail",
                        ["component"] = "PurchasesController",
                        ["operation"] = "list_purchases_by_date",
                        ["db_operation"] = "read_purchases_by_date",
                        ["outcome"] = "failed",
                        ["start_date"] = start?.ToString("O") ?? "",
                        ["end_date"] = end?.ToString("O") ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpPut]
        [Route("[action]/{id}")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Employee) })]
        public IActionResult Approve(int id, [FromBody] PurchaseAuthorizationModel model)
        {
            try
            {
                var purchaseDetail = _purchasesManager.ApprobePurchaseDetail(id, model.pharmacyId, model.drugCode);
                var purchaseDetailModelResponse = new PurchaseDetailModelResponse(id, purchaseDetail);
                _customMetrics.RecordPurchaseStatusChange("approved");
                _customMetrics.RecordBusinessEvent("purchase_status_change", "approved_success");
                _structuredLogger.LogInformation(
                    "Purchase detail approved",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_approve",
                        ["component"] = "PurchasesController",
                        ["operation"] = "approve_purchase_detail",
                        ["db_operation"] = "update_purchase_detail_and_drug_stock",
                        ["outcome"] = "success",
                        ["purchase_id"] = id,
                        ["pharmacy_id"] = model.pharmacyId,
                        ["drug_code"] = model.drugCode,
                        ["purchase_detail_status"] = purchaseDetail.Status
                    });
                return Ok(purchaseDetailModelResponse);
            }
            catch (Exception ex)
            {
                _customMetrics.RecordBusinessEvent("purchase_status_change", "approved_failed");
                _structuredLogger.LogWarning(
                    "Purchase detail approval failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_approve_fail",
                        ["component"] = "PurchasesController",
                        ["operation"] = "approve_purchase_detail",
                        ["db_operation"] = "update_purchase_detail_and_drug_stock",
                        ["outcome"] = "failed",
                        ["purchase_id"] = id,
                        ["pharmacy_id"] = model?.pharmacyId ?? 0,
                        ["drug_code"] = model?.drugCode ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpPut]
        [Route("[action]/{id}")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Employee) })]
        public IActionResult Reject(int id, [FromBody] PurchaseAuthorizationModel model)
        {
            try
            {
                var purchaseDetail = _purchasesManager.RejectPurchaseDetail(id, model.pharmacyId, model.drugCode);
                var purchaseDetailModelResponse = new PurchaseDetailModelResponse(id, purchaseDetail);
                _customMetrics.RecordPurchaseStatusChange("rejected");
                _customMetrics.RecordBusinessEvent("purchase_status_change", "rejected_success");
                _structuredLogger.LogInformation(
                    "Purchase detail rejected",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_reject",
                        ["component"] = "PurchasesController",
                        ["operation"] = "reject_purchase_detail",
                        ["db_operation"] = "update_purchase_detail_and_purchase_total",
                        ["outcome"] = "success",
                        ["purchase_id"] = id,
                        ["pharmacy_id"] = model.pharmacyId,
                        ["drug_code"] = model.drugCode,
                        ["purchase_detail_status"] = purchaseDetail.Status
                    });
                return Ok(purchaseDetailModelResponse);
            }
            catch (Exception ex)
            {
                _customMetrics.RecordBusinessEvent("purchase_status_change", "rejected_failed");
                _structuredLogger.LogWarning(
                    "Purchase detail rejection failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_reject_fail",
                        ["component"] = "PurchasesController",
                        ["operation"] = "reject_purchase_detail",
                        ["db_operation"] = "update_purchase_detail_and_purchase_total",
                        ["outcome"] = "failed",
                        ["purchase_id"] = id,
                        ["pharmacy_id"] = model?.pharmacyId ?? 0,
                        ["drug_code"] = model?.drugCode ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpPost]
        public IActionResult CreatePurchase([FromBody] PurchaseModelRequest purchaseModel)
        {
            try
            {
                var converter = new PurchaseModelRequestToPurchaseConverter();
                var purchase = _purchasesManager.CreatePurchase(converter.Convert(purchaseModel));
                var itemCount = purchase.details?.Sum(detail => detail.Quantity) ?? 0;
                _customMetrics.RecordPurchaseCreated(purchase.TotalAmount, itemCount);
                _customMetrics.RecordBusinessEvent("purchase_create", "success");
                _structuredLogger.LogInformation(
                    "Purchase created",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_create",
                        ["component"] = "PurchasesController",
                        ["operation"] = "create_purchase",
                        ["db_operation"] = "insert_purchase",
                        ["outcome"] = "success",
                        ["order_id"] = purchase.Id,
                        ["tracking_code"] = purchase.TrackingCode,
                        ["buyer_email"] = purchase.BuyerEmail,
                        ["details_count"] = purchase.details?.Count ?? 0
                    });
                var purchaseModelResponse = new PurchaseModelResponse(purchase);
                return Ok(purchaseModelResponse);
            }
            catch (Exception ex)
            {
                _customMetrics.RecordBusinessEvent("purchase_create", "failed");
                _structuredLogger.LogWarning(
                    "Purchase creation failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_create_fail",
                        ["component"] = "PurchasesController",
                        ["operation"] = "create_purchase",
                        ["db_operation"] = "insert_purchase",
                        ["outcome"] = "failed",
                        ["buyer_email"] = purchaseModel?.BuyerEmail ?? "",
                        ["details_count"] = purchaseModel?.Details?.Count ?? 0,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpGet]
        [Route("[action]")]
        public IActionResult Tracking([FromQuery] string? Code)
        {
            try
            {
                var purchase = _purchasesManager.GetPurchaseByTrackingCode(Code);
                var purchaseModelResponse = new PurchaseModelResponse(purchase);
                _structuredLogger.LogInformation(
                    "Purchase tracked",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_tracking",
                        ["component"] = "PurchasesController",
                        ["operation"] = "track_purchase",
                        ["db_operation"] = "read_purchase_by_tracking_code",
                        ["outcome"] = "success",
                        ["tracking_code"] = Code ?? "",
                        ["purchase_id"] = purchase.Id
                    });
                return Ok(purchaseModelResponse);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Purchase tracking failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "purchase_tracking_fail",
                        ["component"] = "PurchasesController",
                        ["operation"] = "track_purchase",
                        ["db_operation"] = "read_purchase_by_tracking_code",
                        ["outcome"] = "failed",
                        ["tracking_code"] = Code ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }
    }
}
