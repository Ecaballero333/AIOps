using ExportationModel.ExportDomain;
using PharmaGo.Domain.Entities;
using PharmaGo.Domain.SearchCriterias;
using PharmaGo.Exceptions;
using PharmaGo.PharmacyService.IBusinessLogic;
using PharmaGo.IDataAccess;
using InstrumentationInterface;

namespace PharmaGo.PharmacyService.BusinessLogic
{
    public class DrugManager : IDrugManager
    {
        private readonly IRepository<Drug> _drugRepository;
        private readonly IRepository<Pharmacy> _pharmacyRepository;
        private readonly IRepository<UnitMeasure> _unitMeasureRepository;
        private readonly IRepository<Presentation> _presentationRepository;
        private readonly IRepository<Session> _sessionRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IStructuredLogger _structuredLogger;

        public DrugManager(IRepository<Drug> drugRepo, 
                           IRepository<Pharmacy> pharmacyRepository, 
                           IRepository<UnitMeasure> unitMeasureRepository, 
                           IRepository<Presentation> presentationRepository,
                           IRepository<Session> sessionRespository,
                           IRepository<User> userRespository,
                           IStructuredLogger structuredLogger)
        {
            _drugRepository = drugRepo;
            _pharmacyRepository = pharmacyRepository;
            _unitMeasureRepository = unitMeasureRepository;
            _presentationRepository = presentationRepository;
            _sessionRepository = sessionRespository;
            _userRepository = userRespository;
            _structuredLogger = structuredLogger;
        }

        public IEnumerable<Drug> GetAll(DrugSearchCriteria drugSearchCriteria)
        {
            Drug drugToSearch = new Drug();
            if (drugSearchCriteria.PharmacyId == null)
            {
                drugToSearch.Name = drugSearchCriteria.Name;
            }
            else
            {
                Pharmacy pharmacySaved = _pharmacyRepository.GetOneByExpression(p => p.Id == drugSearchCriteria.PharmacyId);
                if(pharmacySaved != null)
                {
                    drugToSearch.Name = drugSearchCriteria.Name;
                    drugToSearch.Pharmacy = pharmacySaved;
                }
                else
                {
                    throw new ResourceNotFoundException("The pharmacy to get drugs of does not exist.");
                }
            }
            return _drugRepository.GetAllByExpression(drugSearchCriteria.Criteria(drugToSearch));
        }

        public Drug GetById(int id)
        {
            Drug retrievedDrug = _drugRepository.GetOneByExpression(d => d.Id == id);
            if (retrievedDrug == null)
            {
                throw new ResourceNotFoundException("The drug does not exist.");
            }

            return retrievedDrug;
        }

        public Drug Create(Drug drug, string token)
        {
            Pharmacy pharmacyOfDrug;
            UnitMeasure unitMeasureOfDrug;
            Presentation presentationOfDrug;
            try
            {
                if (drug == null)
                {
                    throw new ResourceNotFoundException("Please create a drug before inserting it.");
                }
                drug.ValidOrFail();

                var guidToken = new Guid(token);
                Session session = _sessionRepository.GetOneByExpression(s => s.Token == guidToken);
                var userId = session.UserId;
                User user = _userRepository.GetOneDetailByExpression(u => u.Id == userId);

                pharmacyOfDrug = _pharmacyRepository.GetOneByExpression(p => p.Name == user.Pharmacy.Name);
                if (pharmacyOfDrug == null)
                {
                    throw new ResourceNotFoundException("The pharmacy of the drug does not exist.");
                }

                if (_drugRepository.Exists(d => d.Code == drug.Code && d.Pharmacy.Name == pharmacyOfDrug.Name))
                {
                    throw new InvalidResourceException("The drug already exists in that pharmacy.");
                }
                unitMeasureOfDrug = _unitMeasureRepository.GetOneByExpression(u => u.Id == drug.UnitMeasure.Id);
                if (unitMeasureOfDrug == null)
                {
                    throw new ResourceNotFoundException("The unit measure of the drug does not exist.");
                }
                presentationOfDrug = _presentationRepository.GetOneByExpression(p => p.Id == drug.Presentation.Id);
                if (presentationOfDrug == null)
                {
                    throw new ResourceNotFoundException("The presentation of the drug does not exist.");
                }

                _structuredLogger.LogInformation(
                    "Drug create business validation completed",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_business_validation",
                        ["component"] = "DrugManager",
                        ["operation"] = "create_drug",
                        ["outcome"] = "success",
                        ["drug_code"] = drug.Code,
                        ["drug_name"] = drug.Name,
                        ["pharmacy_id"] = pharmacyOfDrug.Id,
                        ["pharmacy_name"] = pharmacyOfDrug.Name,
                        ["unit_measure_id"] = unitMeasureOfDrug.Id,
                        ["unit_measure_name"] = unitMeasureOfDrug.Name,
                        ["presentation_id"] = presentationOfDrug.Id,
                        ["presentation_name"] = presentationOfDrug.Name
                    });
            }
            catch (ResourceNotFoundException ex)
            {
                _structuredLogger.LogError(
                    "Drug create business validation failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_business_validation_fail",
                        ["component"] = "DrugManager",
                        ["operation"] = "create_drug",
                        ["outcome"] = "failed",
                        ["drug_code"] = drug?.Code ?? "unknown",
                        ["drug_name"] = drug?.Name ?? "unknown",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
            catch (InvalidResourceException ex)
            {
                _structuredLogger.LogError(
                    "Drug create business validation failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_business_validation_fail",
                        ["component"] = "DrugManager",
                        ["operation"] = "create_drug",
                        ["outcome"] = "failed",
                        ["drug_code"] = drug?.Code ?? "unknown",
                        ["drug_name"] = drug?.Name ?? "unknown",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
            catch (Exception ex)
            {
                _structuredLogger.LogError(
                    "Drug database lookup failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_db_lookup_fail",
                        ["component"] = "DrugManager",
                        ["operation"] = "create_drug",
                        ["db_operation"] = "lookup_drug_dependencies",
                        ["outcome"] = "failed",
                        ["drug_code"] = drug?.Code ?? "unknown",
                        ["drug_name"] = drug?.Name ?? "unknown",
                        ["error_type"] = ex.GetType().Name,
                        ["error_message"] = ex.Message
                    });
                throw;
            }

            drug.UnitMeasure.Id = unitMeasureOfDrug.Id;
            drug.UnitMeasure.Name = unitMeasureOfDrug.Name;
            drug.Presentation.Id = presentationOfDrug.Id;
            drug.Presentation.Name = presentationOfDrug.Name;
            drug.Pharmacy.Id = pharmacyOfDrug.Id;
            try
            {
                _drugRepository.InsertOne(drug);
                _drugRepository.Save();
                _structuredLogger.LogInformation(
                    "Drug persisted in database",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_db_insert",
                        ["component"] = "DrugManager",
                        ["operation"] = "create_drug",
                        ["db_operation"] = "insert_drug",
                        ["outcome"] = "success",
                        ["drug_id"] = drug.Id,
                        ["drug_code"] = drug.Code,
                        ["drug_name"] = drug.Name,
                        ["pharmacy_id"] = pharmacyOfDrug.Id,
                        ["pharmacy_name"] = pharmacyOfDrug.Name
                    });
            }
            catch (Exception ex)
            {
                _structuredLogger.LogError(
                    "Drug database insert failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_db_insert_fail",
                        ["component"] = "DrugManager",
                        ["operation"] = "create_drug",
                        ["db_operation"] = "insert_drug",
                        ["outcome"] = "failed",
                        ["drug_code"] = drug.Code,
                        ["drug_name"] = drug.Name,
                        ["pharmacy_id"] = pharmacyOfDrug.Id,
                        ["pharmacy_name"] = pharmacyOfDrug.Name,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
            return drug;
        }

        public Drug Update(int id, Drug updatedDrug) 
        {
            Drug drugSaved;
            try
            {
                if(updatedDrug == null)
                {
                    throw new ResourceNotFoundException("The updated drug is invalid.");
                }
                updatedDrug.ValidOrFail();
                drugSaved = _drugRepository.GetOneByExpression(d => d.Id == id);
                if(drugSaved == null)
                {
                    throw new ResourceNotFoundException("The drug to update does not exist.");
                }
                _structuredLogger.LogInformation(
                    "Drug update business validation completed",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_business_validation",
                        ["component"] = "DrugManager",
                        ["operation"] = "update_drug",
                        ["outcome"] = "success",
                        ["drug_id"] = id,
                        ["drug_code"] = updatedDrug.Code,
                        ["drug_name"] = updatedDrug.Name
                    });
            }
            catch (ResourceNotFoundException ex)
            {
                LogDrugBusinessFailure("update_drug", id, updatedDrug?.Code, updatedDrug?.Name, ex);
                throw;
            }
            catch (InvalidResourceException ex)
            {
                LogDrugBusinessFailure("update_drug", id, updatedDrug?.Code, updatedDrug?.Name, ex);
                throw;
            }
            catch (Exception ex)
            {
                LogDrugLookupFailure("update_drug", "lookup_drug", id, updatedDrug?.Code, updatedDrug?.Name, ex);
                throw;
            }
            drugSaved.Code = updatedDrug.Code;
            drugSaved.Name = updatedDrug.Name;
            drugSaved.Symptom = updatedDrug.Symptom;
            drugSaved.Quantity = updatedDrug.Quantity;
            drugSaved.Price = updatedDrug.Price;
            drugSaved.Prescription = updatedDrug.Prescription;
            try
            {
                _drugRepository.UpdateOne(drugSaved);
                _drugRepository.Save();
                _structuredLogger.LogInformation(
                    "Drug updated in database",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_db_update",
                        ["component"] = "DrugManager",
                        ["operation"] = "update_drug",
                        ["db_operation"] = "update_drug",
                        ["outcome"] = "success",
                        ["drug_id"] = drugSaved.Id,
                        ["drug_code"] = drugSaved.Code,
                        ["drug_name"] = drugSaved.Name
                    });
            }
            catch (Exception ex)
            {
                LogDrugPersistenceFailure("drug_db_update_fail", "update_drug", "update_drug", drugSaved.Id, drugSaved.Code, drugSaved.Name, ex);
                throw;
            }
            return drugSaved;
        }

        public void Delete(int id)
        {
            Drug drugSaved;
            try
            {
                drugSaved = _drugRepository.GetOneByExpression(d => d.Id == id);
                if(drugSaved == null)
                {
                    throw new ResourceNotFoundException("The drug to delete does not exist.");
                }
            }
            catch (ResourceNotFoundException ex)
            {
                LogDrugBusinessFailure("delete_drug", id, null, null, ex);
                throw;
            }
            catch (Exception ex)
            {
                LogDrugLookupFailure("delete_drug", "lookup_drug", id, null, null, ex);
                throw;
            }
            drugSaved.Deleted = true;
            try
            {
                _drugRepository.UpdateOne(drugSaved);
                _drugRepository.Save();
                _structuredLogger.LogInformation(
                    "Drug deleted in database",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_db_delete",
                        ["component"] = "DrugManager",
                        ["operation"] = "delete_drug",
                        ["db_operation"] = "delete_drug",
                        ["outcome"] = "success",
                        ["drug_id"] = drugSaved.Id,
                        ["drug_code"] = drugSaved.Code,
                        ["drug_name"] = drugSaved.Name
                    });
            }
            catch (Exception ex)
            {
                LogDrugPersistenceFailure("drug_db_delete_fail", "delete_drug", "delete_drug", drugSaved.Id, drugSaved.Code, drugSaved.Name, ex);
                throw;
            }
        }

        private void LogDrugBusinessFailure(string operation, int drugId, string drugCode, string drugName, Exception ex)
        {
            _structuredLogger.LogError(
                "Drug business validation failed",
                ex,
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = "drug_business_validation_fail",
                    ["component"] = "DrugManager",
                    ["operation"] = operation,
                    ["outcome"] = "failed",
                    ["drug_id"] = drugId,
                    ["drug_code"] = drugCode ?? "unknown",
                    ["drug_name"] = drugName ?? "unknown",
                    ["error_message"] = ex.Message
                });
        }

        private void LogDrugLookupFailure(string operation, string dbOperation, int drugId, string drugCode, string drugName, Exception ex)
        {
            _structuredLogger.LogError(
                "Drug database lookup failed",
                ex,
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = "drug_db_lookup_fail",
                    ["component"] = "DrugManager",
                    ["operation"] = operation,
                    ["db_operation"] = dbOperation,
                    ["outcome"] = "failed",
                    ["drug_id"] = drugId,
                    ["drug_code"] = drugCode ?? "unknown",
                    ["drug_name"] = drugName ?? "unknown",
                    ["error_type"] = ex.GetType().Name,
                    ["error_message"] = ex.Message
                });
        }

        private void LogDrugPersistenceFailure(string pharmaBiz, string operation, string dbOperation, int drugId, string drugCode, string drugName, Exception ex)
        {
            _structuredLogger.LogError(
                "Drug database operation failed",
                ex,
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = pharmaBiz,
                    ["component"] = "DrugManager",
                    ["operation"] = operation,
                    ["db_operation"] = dbOperation,
                    ["outcome"] = "failed",
                    ["drug_id"] = drugId,
                    ["drug_code"] = drugCode ?? "unknown",
                    ["drug_name"] = drugName ?? "unknown",
                    ["error_type"] = ex.GetType().Name,
                    ["error_message"] = ex.Message
                });
        }

        public IEnumerable<DrugExportationModel> GetDrugsToExport(string token)
        {
            var guidToken = new Guid(token);
            Session session = _sessionRepository.GetOneByExpression(s => s.Token == guidToken);
            var userId = session.UserId;
            User user = _userRepository.GetOneDetailByExpression(u => u.Id == userId);
            Pharmacy pharmacyOfDrug = _pharmacyRepository.GetOneByExpression(p => p.Name == user.Pharmacy.Name);
            IEnumerable<Drug> drugsSaved = _drugRepository.GetAllByExpression(d => d.Name == d.Name && d.Pharmacy.Id == pharmacyOfDrug.Id);
            IEnumerable<DrugExportationModel> drugsToExport = drugsSaved.Select(d =>
                new DrugExportationModel
                {
                    Code = d.Code,
                    Name = d.Name,
                    Symptom = d.Symptom,
                    Deleted = d.Deleted
                }).ToList();
            return drugsToExport;
        }

        public IEnumerable<Drug> GetAllByUser(string token)
        {
            var guidToken = new Guid(token);
            Session session = _sessionRepository.GetOneByExpression(s => s.Token == guidToken);
            var userId = session.UserId;
            User user = _userRepository.GetOneDetailByExpression(u => u.Id == userId);
            Pharmacy pharmacy = user.Pharmacy;
            return _drugRepository.GetAllByExpression(d => d.Deleted == false && d.Pharmacy.Id == pharmacy.Id);
        }
    }
}

