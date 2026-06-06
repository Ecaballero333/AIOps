using PharmaGo.Domain.Entities;
using PharmaGo.Exceptions;
using PharmaGo.UsersService.IBusinessLogic;
using PharmaGo.IDataAccess;
using InstrumentationInterface;
using System.Text.RegularExpressions;

namespace PharmaGo.UsersService.BusinessLogic
{
    public class UsersManager : IUsersManager
    {
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Invitation> _invitationRepository;
        private readonly IStructuredLogger _structuredLogger;

        public UsersManager(IRepository<User> repository, IRepository<Invitation> invitationRepository, IStructuredLogger structuredLogger)
        {
            _userRepository = repository;
            _invitationRepository = invitationRepository;
            _structuredLogger = structuredLogger;
        }

        public User CreateUser(string UserName, string UserCode, string Email, string Password, string Address, DateTime RegistrationDate)
        {
            string validUserCode = @"^[0-9]{6}$";
            Regex rgUserCode = new(validUserCode);
            string validEmail = @"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$";
            Regex rgEmail = new(validEmail);
            string validPassword = @"^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&.*-]).{8,}$";
            Regex rgPassword = new(validPassword);

            if (String.IsNullOrEmpty(UserName)) throw new InvalidResourceException("Invalid Username");
            if (String.IsNullOrEmpty(UserCode) || !rgUserCode.IsMatch(UserCode)) throw new InvalidResourceException("Invalid UserCode");
            if (String.IsNullOrEmpty(Email) || !rgEmail.IsMatch(Email)) throw new InvalidResourceException("Invalid Email");
            if (String.IsNullOrEmpty(Password) || !rgPassword.IsMatch(Password)) throw new InvalidResourceException("Invalid Password");
            if (String.IsNullOrEmpty(Address)) throw new InvalidResourceException("Invalid Address");

            Invitation invitation = _invitationRepository.GetOneDetailByExpression(x => x.UserName.ToLower() == UserName.ToLower() && x.UserCode == UserCode && x.IsActive);
            if (invitation == null)
                throw new ResourceNotFoundException("Invitation not found or is not currently active");
            User exists = _userRepository.GetOneByExpression(u => u.UserName.ToLower() == UserName.ToLower());
            if (exists != null)
                throw new InvalidResourceException("Invalid Username, Username already exists");
            exists = _userRepository.GetOneByExpression(u => u.Email.ToLower() == Email.ToLower());
            if (exists != null)
                throw new InvalidResourceException("Invalid Email, Email already exists");

            User user = new User
            {
                UserName = UserName,
                Email = Email,
                Address = Address,
                Password = Password,
                RegistrationDate = RegistrationDate,
                Pharmacy = invitation.Pharmacy,
                Role = invitation.Role
            };
            _structuredLogger.LogInformation(
                "User create business validation completed",
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = "user_business_validation",
                    ["component"] = "UsersManager",
                    ["operation"] = "create_user",
                    ["outcome"] = "success",
                    ["user_name"] = UserName,
                    ["email"] = Email,
                    ["invitation_id"] = invitation.Id
                });

            try
            {
                _userRepository.InsertOne(user);

                invitation.IsActive = false;
                _invitationRepository.UpdateOne(invitation);

                _userRepository.Save();
                _invitationRepository.Save();
                _structuredLogger.LogInformation(
                    "User persisted in database",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "user_db_insert",
                        ["component"] = "UsersManager",
                        ["operation"] = "create_user",
                        ["db_operation"] = "insert_user_and_update_invitation",
                        ["outcome"] = "success",
                        ["user_id"] = user.Id,
                        ["user_name"] = user.UserName,
                        ["invitation_id"] = invitation.Id
                    });
            }
            catch (Exception ex)
            {
                _structuredLogger.LogError(
                    "User database insert failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "user_db_insert_fail",
                        ["component"] = "UsersManager",
                        ["operation"] = "create_user",
                        ["db_operation"] = "insert_user_and_update_invitation",
                        ["outcome"] = "failed",
                        ["user_name"] = UserName ?? "unknown",
                        ["email"] = Email ?? "unknown",
                        ["error_type"] = ex.GetType().Name,
                        ["error_message"] = ex.Message
                    });
                throw;
            }

            return user;

        }
    }

}

