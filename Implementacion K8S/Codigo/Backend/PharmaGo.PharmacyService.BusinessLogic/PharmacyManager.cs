using PharmaGo.Domain.Entities;
using PharmaGo.Domain.SearchCriterias;
using PharmaGo.Exceptions;
using PharmaGo.PharmacyService.IBusinessLogic;
using PharmaGo.IDataAccess;
using InstrumentationInterface;

namespace PharmaGo.PharmacyService.BusinessLogic
{
    public class PharmacyManager : IPharmacyManager
    {
        private readonly IRepository<Pharmacy> _pharmacyRepository;
        private readonly IStructuredLogger _structuredLogger;

        public PharmacyManager(IRepository<Pharmacy> pharmacyRepository, IStructuredLogger structuredLogger)
        {
            _pharmacyRepository = pharmacyRepository;
            _structuredLogger = structuredLogger;
        }

        public IEnumerable<Pharmacy> GetAll(PharmacySearchCriteria pharmacySearchCriteria)
        {
            Pharmacy pharmacyToSearch = new Pharmacy 
            { 
                Name = pharmacySearchCriteria.Name, 
                Address = pharmacySearchCriteria.Address 
            };
            return _pharmacyRepository.GetAllByExpression(pharmacySearchCriteria.Criteria(pharmacyToSearch));
        }

        public Pharmacy GetById(int id)
        {
            Pharmacy pharmacySaved = _pharmacyRepository.GetOneByExpression(p => p.Id == id);
            if(pharmacySaved == null)
            {
                throw new ResourceNotFoundException("The pharmacy does not exist.");
            }
            return pharmacySaved;
        }

        public Pharmacy Create(Pharmacy pharmacy)
        {
            try
            {
                if(pharmacy == null)
                {
                    throw new InvalidResourceException("The pharmacy to create is invalid.");
                }
                pharmacy.ValidOrFail();
                Pharmacy pharmacySaved = _pharmacyRepository.GetOneByExpression(p => p.Name == pharmacy.Name);
                if(pharmacySaved != null)
                {
                    throw new InvalidResourceException("The pharmacy already exist.");
                }
                _structuredLogger.LogInformation(
                    "Pharmacy create business validation completed",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_business_validation",
                        ["component"] = "PharmacyManager",
                        ["operation"] = "create_pharmacy",
                        ["outcome"] = "success",
                        ["pharmacy_name"] = pharmacy.Name
                    });
            }
            catch (InvalidResourceException ex)
            {
                _structuredLogger.LogError(
                    "Pharmacy create business validation failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_business_validation_fail",
                        ["component"] = "PharmacyManager",
                        ["operation"] = "create_pharmacy",
                        ["outcome"] = "failed",
                        ["pharmacy_name"] = pharmacy?.Name ?? "unknown",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
            catch (Exception ex)
            {
                _structuredLogger.LogError(
                    "Pharmacy database lookup failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_db_lookup_fail",
                        ["component"] = "PharmacyManager",
                        ["operation"] = "create_pharmacy",
                        ["db_operation"] = "lookup_pharmacy",
                        ["outcome"] = "failed",
                        ["pharmacy_name"] = pharmacy?.Name ?? "unknown",
                        ["error_type"] = ex.GetType().Name,
                        ["error_message"] = ex.Message
                    });
                throw;
            }

            try
            {
                _pharmacyRepository.InsertOne(pharmacy);
                _pharmacyRepository.Save();
                _structuredLogger.LogInformation(
                    "Pharmacy persisted in database",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_db_insert",
                        ["component"] = "PharmacyManager",
                        ["operation"] = "create_pharmacy",
                        ["db_operation"] = "insert_pharmacy",
                        ["outcome"] = "success",
                        ["pharmacy_id"] = pharmacy.Id,
                        ["pharmacy_name"] = pharmacy.Name
                    });
            }
            catch (Exception ex)
            {
                _structuredLogger.LogError(
                    "Pharmacy database insert failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_db_insert_fail",
                        ["component"] = "PharmacyManager",
                        ["operation"] = "create_pharmacy",
                        ["db_operation"] = "insert_pharmacy",
                        ["outcome"] = "failed",
                        ["pharmacy_name"] = pharmacy?.Name ?? "unknown",
                        ["error_type"] = ex.GetType().Name,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
            return pharmacy;
        }

        public Pharmacy Update(int id, Pharmacy updatedPharmacy)
        {
            Pharmacy pharmacySaved;
            try
            {
                if (updatedPharmacy == null)
                {
                    throw new InvalidResourceException("The updatedPharmacy is invalid.");
                }
                updatedPharmacy.ValidOrFail();
                pharmacySaved = _pharmacyRepository.GetOneByExpression(p => p.Id == id);
                if (pharmacySaved == null)
                {
                    throw new ResourceNotFoundException("The pharmacy to update does not exist.");
                }
                _structuredLogger.LogInformation(
                    "Pharmacy update business validation completed",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_business_validation",
                        ["component"] = "PharmacyManager",
                        ["operation"] = "update_pharmacy",
                        ["outcome"] = "success",
                        ["pharmacy_id"] = id
                    });
            }
            catch (InvalidResourceException ex)
            {
                LogPharmacyBusinessFailure("update_pharmacy", id, updatedPharmacy?.Name, ex);
                throw;
            }
            catch (ResourceNotFoundException ex)
            {
                LogPharmacyBusinessFailure("update_pharmacy", id, updatedPharmacy?.Name, ex);
                throw;
            }
            catch (Exception ex)
            {
                LogPharmacyLookupFailure("update_pharmacy", "lookup_pharmacy", id, updatedPharmacy?.Name, ex);
                throw;
            }

            pharmacySaved.Name = updatedPharmacy.Name;
            pharmacySaved.Address = updatedPharmacy.Address;
            try
            {
                _pharmacyRepository.UpdateOne(pharmacySaved);
                _pharmacyRepository.Save();
                _structuredLogger.LogInformation(
                    "Pharmacy updated in database",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_db_update",
                        ["component"] = "PharmacyManager",
                        ["operation"] = "update_pharmacy",
                        ["db_operation"] = "update_pharmacy",
                        ["outcome"] = "success",
                        ["pharmacy_id"] = pharmacySaved.Id,
                        ["pharmacy_name"] = pharmacySaved.Name
                    });
            }
            catch (Exception ex)
            {
                LogPharmacyPersistenceFailure("pharmacy_db_update_fail", "update_pharmacy", "update_pharmacy", pharmacySaved.Id, pharmacySaved.Name, ex);
                throw;
            }
            return pharmacySaved;
        }

        public void Delete(int id)
        {
            Pharmacy pharmacySaved;
            try
            {
                pharmacySaved = _pharmacyRepository.GetOneByExpression(p => p.Id == id);
                if (pharmacySaved == null)
                {
                    throw new ResourceNotFoundException("The pharmacy to delete does not exist.");
                }
            }
            catch (ResourceNotFoundException ex)
            {
                LogPharmacyBusinessFailure("delete_pharmacy", id, null, ex);
                throw;
            }
            catch (Exception ex)
            {
                LogPharmacyLookupFailure("delete_pharmacy", "lookup_pharmacy", id, null, ex);
                throw;
            }

            try
            {
                _pharmacyRepository.DeleteOne(pharmacySaved);
                _pharmacyRepository.Save();
                _structuredLogger.LogInformation(
                    "Pharmacy deleted from database",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "pharmacy_db_delete",
                        ["component"] = "PharmacyManager",
                        ["operation"] = "delete_pharmacy",
                        ["db_operation"] = "delete_pharmacy",
                        ["outcome"] = "success",
                        ["pharmacy_id"] = pharmacySaved.Id,
                        ["pharmacy_name"] = pharmacySaved.Name
                    });
            }
            catch (Exception ex)
            {
                LogPharmacyPersistenceFailure("pharmacy_db_delete_fail", "delete_pharmacy", "delete_pharmacy", pharmacySaved.Id, pharmacySaved.Name, ex);
                throw;
            }
        }

        private void LogPharmacyBusinessFailure(string operation, int pharmacyId, string pharmacyName, Exception ex)
        {
            _structuredLogger.LogError(
                "Pharmacy business validation failed",
                ex,
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = "pharmacy_business_validation_fail",
                    ["component"] = "PharmacyManager",
                    ["operation"] = operation,
                    ["outcome"] = "failed",
                    ["pharmacy_id"] = pharmacyId,
                    ["pharmacy_name"] = pharmacyName ?? "unknown",
                    ["error_message"] = ex.Message
                });
        }

        private void LogPharmacyLookupFailure(string operation, string dbOperation, int pharmacyId, string pharmacyName, Exception ex)
        {
            _structuredLogger.LogError(
                "Pharmacy database lookup failed",
                ex,
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = "pharmacy_db_lookup_fail",
                    ["component"] = "PharmacyManager",
                    ["operation"] = operation,
                    ["db_operation"] = dbOperation,
                    ["outcome"] = "failed",
                    ["pharmacy_id"] = pharmacyId,
                    ["pharmacy_name"] = pharmacyName ?? "unknown",
                    ["error_type"] = ex.GetType().Name,
                    ["error_message"] = ex.Message
                });
        }

        private void LogPharmacyPersistenceFailure(string pharmaBiz, string operation, string dbOperation, int pharmacyId, string pharmacyName, Exception ex)
        {
            _structuredLogger.LogError(
                "Pharmacy database operation failed",
                ex,
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = pharmaBiz,
                    ["component"] = "PharmacyManager",
                    ["operation"] = operation,
                    ["db_operation"] = dbOperation,
                    ["outcome"] = "failed",
                    ["pharmacy_id"] = pharmacyId,
                    ["pharmacy_name"] = pharmacyName ?? "unknown",
                    ["error_type"] = ex.GetType().Name,
                    ["error_message"] = ex.Message
                });
        }
    }
}

