namespace CafeteriaInsti.Services
{
    /// <summary>
    /// Servicio para mantener la sesión del usuario activo
    /// </summary>
    public class SessionService
    {
        private string _currentUserEmail;
        private UserCredentials _currentUser;

        /// <summary>
        /// Email del usuario actualmente logueado
        /// </summary>
        public string CurrentUserEmail
        {
            get => _currentUserEmail;
            private set => _currentUserEmail = value;
        }

        /// <summary>
        /// Datos del usuario actualmente logueado
        /// </summary>
        public UserCredentials CurrentUser
        {
            get => _currentUser;
            private set => _currentUser = value;
        }

        /// <summary>
        /// Indica si hay un usuario logueado
        /// </summary>
        public bool IsLoggedIn => !string.IsNullOrEmpty(CurrentUserEmail);

        /// <summary>
        /// Establece la sesión actual del usuario
        /// </summary>
        public void SetSession(string email, UserCredentials user)
        {
            CurrentUserEmail = email;
            CurrentUser = user;
        }

        /// <summary>
        /// Limpia la sesión actual
        /// </summary>
        public void ClearSession()
        {
            CurrentUserEmail = null;
            CurrentUser = null;
        }
    }
}
