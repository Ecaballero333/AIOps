using PharmaGo.Domain.Entities;
using PharmaGo.Exceptions;
using PharmaGo.UsersService.IBusinessLogic;
using PharmaGo.IDataAccess;
using InstrumentationInterface;

namespace PharmaGo.UsersService.BusinessLogic
{
    public class LoginManager : ILoginManager
    {
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Session> _sessionRepository;
        private readonly IStructuredLogger _structuredLogger;

        public LoginManager(IRepository<User> userRepository, IRepository<Session> sessionRepository, IStructuredLogger structuredLogger)
        {
            _userRepository = userRepository;
            _sessionRepository = sessionRepository;
            _structuredLogger = structuredLogger;
        }

        public Authorization Login(string userName, string password)
        {
            User user;
            Session session;
            try
            {
                if (String.IsNullOrEmpty(userName))
                {
                    throw new InvalidResourceException("Invalid Username");
                }
                user = _userRepository.GetOneDetailByExpression(u => u.UserName.ToLower().Equals(userName.ToLower()));
                if (user == null) {
                    throw new ResourceNotFoundException("The user does not exist");
                }
                if (String.IsNullOrEmpty(password) || !user.Password.Equals(password)) {
                    throw new InvalidResourceException("Invalid Password");
                }
                var _userId = user.Id;
                session = _sessionRepository.GetOneByExpression(s => s.UserId == _userId);
                _structuredLogger.LogInformation(
                    "Login business validation completed",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "login_business_validation",
                        ["component"] = "LoginManager",
                        ["operation"] = "login",
                        ["outcome"] = "success",
                        ["user_id"] = user.Id,
                        ["user_name"] = user.UserName
                    });
            }
            catch (InvalidResourceException ex)
            {
                LogLoginBusinessFailure(userName, ex);
                throw;
            }
            catch (ResourceNotFoundException ex)
            {
                LogLoginBusinessFailure(userName, ex);
                throw;
            }
            catch (Exception ex)
            {
                _structuredLogger.LogError(
                    "Login database lookup failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "login_db_lookup_fail",
                        ["component"] = "LoginManager",
                        ["operation"] = "login",
                        ["db_operation"] = "lookup_user_session",
                        ["outcome"] = "failed",
                        ["user_name"] = userName ?? "unknown",
                        ["error_type"] = ex.GetType().Name,
                        ["error_message"] = ex.Message
                    });
                throw;
            }

            if (session == null) {
                var token = Guid.NewGuid();
                Session newSession = new Session { Token = token, UserId = user.Id };
                try
                {
                    _sessionRepository.InsertOne(newSession);
                    _sessionRepository.Save();
                    _structuredLogger.LogInformation(
                        "Login session persisted in database",
                        new Dictionary<string, object>
                        {
                            ["pharma_biz"] = "login_session_db_insert",
                            ["component"] = "LoginManager",
                            ["operation"] = "login",
                            ["db_operation"] = "insert_session",
                            ["outcome"] = "success",
                            ["user_id"] = user.Id,
                            ["user_name"] = user.UserName
                        });
                }
                catch (Exception ex)
                {
                    _structuredLogger.LogError(
                        "Login session database insert failed",
                        ex,
                        new Dictionary<string, object>
                        {
                            ["pharma_biz"] = "login_session_db_insert_fail",
                            ["component"] = "LoginManager",
                            ["operation"] = "login",
                            ["db_operation"] = "insert_session",
                            ["outcome"] = "failed",
                            ["user_id"] = user.Id,
                            ["user_name"] = user.UserName,
                            ["error_type"] = ex.GetType().Name,
                            ["error_message"] = ex.Message
                        });
                    throw;
                }
                return new Authorization { Token = token, Role = user.Role.Name, UserName = user.UserName, UserId = user.Id };
            }
            return new Authorization { Token = session.Token, Role = user.Role.Name, UserName = user.UserName, UserId = user.Id };
        }

        public bool IsTokenValid(string token)
        {
            var guidToken = new Guid(token);
            Session session = _sessionRepository.GetOneByExpression(x => x.Token == guidToken);
            if (session == null) return false;
            return true;
        }

        public bool IsRoleValid(string[] roles, string token)
        {
            var guidToken = new Guid(token);
            Session session = _sessionRepository.GetOneByExpression(x => x.Token == guidToken);
            var userId = session.UserId;
            User user = _userRepository.GetOneDetailByExpression(x => x.Id == userId);
            if (user == null) return false;
            foreach(string role in roles)
            {
                if (user.Role.Name.ToLower() == role.ToLower()) return true;
            }
            return false;
        }

        private void LogLoginBusinessFailure(string userName, Exception ex)
        {
            _structuredLogger.LogError(
                "Login business validation failed",
                ex,
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = "login_business_validation_fail",
                    ["component"] = "LoginManager",
                    ["operation"] = "login",
                    ["outcome"] = "failed",
                    ["user_name"] = userName ?? "unknown",
                    ["error_message"] = ex.Message
                });
        }
    }
}

