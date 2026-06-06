using PharmaGo.Domain.Entities;
using PharmaGo.Domain.SearchCriterias;
using PharmaGo.Exceptions;
using PharmaGo.PharmacyService.IBusinessLogic;
using PharmaGo.IDataAccess;
using InstrumentationInterface;

namespace PharmaGo.PharmacyService.BusinessLogic
{
    public class StockRequestManager : IStockRequestManager
    {
        private readonly IRepository<StockRequest> _stockRequestRepository;
        private readonly IRepository<User> _employeeRepository;
        private readonly IRepository<Drug> _drugRepository;
        private readonly IRepository<Session> _sessionRepository;
        private readonly IStructuredLogger _structuredLogger;

        public StockRequestManager(IRepository<StockRequest> stockRequestRepository,
            IRepository<User> employeeRepository, IRepository<Drug> drugRepository, IRepository<Session> sessionRepository,
            IStructuredLogger structuredLogger)
		{
            _stockRequestRepository = stockRequestRepository;
            _employeeRepository = employeeRepository;
            _drugRepository = drugRepository;
            _sessionRepository = sessionRepository;
            _structuredLogger = structuredLogger;
        }

        public bool ApproveStockRequest(int id)
        {
            var stockRequest = _stockRequestRepository.GetOneByExpression(s => s.Id == id);
            if (stockRequest == null) throw new InvalidResourceException("Invalid stock request.");
            if (stockRequest != null)
            {
                if (stockRequest.Status == Domain.Enums.StockRequestStatus.Approved) throw new InvalidResourceException("Stock request already approved.");
                if (stockRequest.Status == Domain.Enums.StockRequestStatus.Rejected) throw new InvalidResourceException("Stock request already rejected.");
            }

            foreach (var stockRequestDetail in stockRequest.Details)
            {
                if (stockRequestDetail.Drug.Deleted == true) throw new InvalidResourceException("Stock request has deleted drugs included.");
                var drug = _drugRepository.GetOneByExpression(d => d.Id == stockRequestDetail.Drug.Id);
                drug.Stock += stockRequestDetail.Quantity;
                _drugRepository.UpdateOne(drug);
            }

            _structuredLogger.LogInformation(
                "Stock request approve business validation completed",
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = "stock_request_business_validation",
                    ["component"] = "StockRequestManager",
                    ["operation"] = "approve_stock_request",
                    ["outcome"] = "success",
                    ["stock_request_id"] = id
                });

            try
            {
                stockRequest.Status = Domain.Enums.StockRequestStatus.Approved;
                _stockRequestRepository.UpdateOne(stockRequest);

                _drugRepository.Save();
                _stockRequestRepository.Save();
                _structuredLogger.LogInformation(
                    "Stock request approval persisted in database",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_db_update",
                        ["component"] = "StockRequestManager",
                        ["operation"] = "approve_stock_request",
                        ["db_operation"] = "update_stock_request_and_drug_stock",
                        ["outcome"] = "success",
                        ["stock_request_id"] = id,
                        ["status"] = stockRequest.Status.ToString()
                    });
            }
            catch (Exception ex)
            {
                LogStockRequestPersistenceFailure("stock_request_db_update_fail", "approve_stock_request", "update_stock_request_and_drug_stock", id, ex);
                throw;
            }

            return true;
        }

        public bool RejectStockRequest(int id)
        {
            var stockRequest = _stockRequestRepository.GetOneByExpression(s => s.Id == id);
            if (stockRequest == null) throw new InvalidResourceException("Invalid stock request.");
            if (stockRequest != null)
            {
                if (stockRequest.Status == Domain.Enums.StockRequestStatus.Approved) throw new InvalidResourceException("Stock request already approved.");
                if (stockRequest.Status == Domain.Enums.StockRequestStatus.Rejected) throw new InvalidResourceException("Stock request already rejected.");
            }  

            _structuredLogger.LogInformation(
                "Stock request reject business validation completed",
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = "stock_request_business_validation",
                    ["component"] = "StockRequestManager",
                    ["operation"] = "reject_stock_request",
                    ["outcome"] = "success",
                    ["stock_request_id"] = id
                });

            try
            {
                stockRequest.Status = Domain.Enums.StockRequestStatus.Rejected;
                _stockRequestRepository.UpdateOne(stockRequest);
                _stockRequestRepository.Save();
                _structuredLogger.LogInformation(
                    "Stock request rejection persisted in database",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_db_update",
                        ["component"] = "StockRequestManager",
                        ["operation"] = "reject_stock_request",
                        ["db_operation"] = "update_stock_request",
                        ["outcome"] = "success",
                        ["stock_request_id"] = id,
                        ["status"] = stockRequest.Status.ToString()
                    });
            }
            catch (Exception ex)
            {
                LogStockRequestPersistenceFailure("stock_request_db_update_fail", "reject_stock_request", "update_stock_request", id, ex);
                throw;
            }

            return true;
        }

        public StockRequest CreateStockRequest(StockRequest stockRequest, string token)
        {
            User existEmployee = null;
            if (stockRequest.Details == null) throw new InvalidResourceException("Invalid stock details.");
            if (stockRequest.Details.Count == 0) throw new InvalidResourceException("Invalid stock details.");

            var session = _sessionRepository.GetOneByExpression(session => session.Token == new Guid(token));
            if (session == null) throw new InvalidResourceException("Invalid session from employee.");
            var userId = session.UserId;

            existEmployee = _employeeRepository.GetOneDetailByExpression(u => u.Id == userId);
            if (existEmployee == null) throw new InvalidResourceException("Invalid employee.");
            stockRequest.Employee = existEmployee;

            foreach (StockRequestDetail item in stockRequest.Details)
            {
                var drug = _drugRepository.GetOneByExpression(d => d.Code == item.Drug.Code && d.Pharmacy.Id == existEmployee.Pharmacy.Id);
                if (drug == null) throw new InvalidResourceException("Stock request has invalid drug.");

                item.Drug = drug;
            }

            _structuredLogger.LogInformation(
                "Stock request create business validation completed",
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = "stock_request_business_validation",
                    ["component"] = "StockRequestManager",
                    ["operation"] = "create_stock_request",
                    ["outcome"] = "success",
                    ["employee_id"] = existEmployee.Id,
                    ["details_count"] = stockRequest.Details.Count
                });

            try
            {
                stockRequest.Status = Domain.Enums.StockRequestStatus.Pending;
                _stockRequestRepository.InsertOne(stockRequest);
                _stockRequestRepository.Save();
                _structuredLogger.LogInformation(
                    "Stock request persisted in database",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "stock_request_db_insert",
                        ["component"] = "StockRequestManager",
                        ["operation"] = "create_stock_request",
                        ["db_operation"] = "insert_stock_request",
                        ["outcome"] = "success",
                        ["stock_request_id"] = stockRequest.Id,
                        ["employee_id"] = existEmployee.Id,
                        ["status"] = stockRequest.Status.ToString()
                    });
            }
            catch (Exception ex)
            {
                LogStockRequestPersistenceFailure("stock_request_db_insert_fail", "create_stock_request", "insert_stock_request", stockRequest.Id, ex);
                throw;
            }

            return stockRequest;
        }

        public IEnumerable<StockRequest> GetStockRequestsByEmployee(string token, StockRequestSearchCriteria searchCriteria)
        {
            if (string.IsNullOrEmpty(token.ToString())) throw new InvalidResourceException("Invalid employee.");
            var session = _sessionRepository.GetOneByExpression(session => session.Token == new Guid(token));
            if (session == null) throw new InvalidResourceException("Invalid employee.");
            searchCriteria.EmployeeId = session.UserId;

            var stockRequests = _stockRequestRepository.GetAllBasicByExpression(searchCriteria.Criteria());

            return stockRequests;
        }

        public IEnumerable<StockRequest> GetStockRequestsByOwner(string token) {

            if (string.IsNullOrEmpty(token.ToString())) throw new InvalidResourceException("Invalid owner.");
            var session = _sessionRepository.GetOneByExpression(session => session.Token == new Guid(token));
            if (session == null) throw new ResourceNotFoundException("Invalid owner.");

            var userId = session.UserId;
            User user = _employeeRepository.GetOneDetailByExpression(u => u.Id == userId);
            if (user == null) throw new ResourceNotFoundException("Invalid user.");
            var pharmacyId = user.Pharmacy.Id;
            var stockRequests = _stockRequestRepository.GetAllBasicByExpression(s => s.Employee.Pharmacy.Id == pharmacyId);

            return stockRequests;

        }

        private void LogStockRequestPersistenceFailure(string pharmaBiz, string operation, string dbOperation, int stockRequestId, Exception ex)
        {
            _structuredLogger.LogError(
                "Stock request database operation failed",
                ex,
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = pharmaBiz,
                    ["component"] = "StockRequestManager",
                    ["operation"] = operation,
                    ["db_operation"] = dbOperation,
                    ["outcome"] = "failed",
                    ["stock_request_id"] = stockRequestId,
                    ["error_type"] = ex.GetType().Name,
                    ["error_message"] = ex.Message
                });
        }
    }
}

