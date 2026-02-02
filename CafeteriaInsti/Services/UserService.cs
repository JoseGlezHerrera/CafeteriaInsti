using System.Text.Json;

namespace CafeteriaInsti.Services
{
    public class UserCredentials
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Turno { get; set; } // "Mañana" o "Tarde"
    }

    public class UserService
    {
        private readonly string _usersFilePath;
        private List<UserCredentials> _users;

        public UserService()
        {
            _usersFilePath = Path.Combine(FileSystem.AppDataDirectory, "users.json");
            _users = new List<UserCredentials>();
            LoadUsers();
        }

        /// <summary>
        /// Carga los usuarios del archivo
        /// </summary>
        private void LoadUsers()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Cargando usuarios desde {_usersFilePath}");

                if (File.Exists(_usersFilePath))
                {
                    var json = File.ReadAllText(_usersFilePath);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Archivo encontrado, contenido: {json.Length} caracteres");

                    _users = JsonSerializer.Deserialize<List<UserCredentials>>(json) ?? new List<UserCredentials>();
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] {_users.Count} usuarios cargados exitosamente");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Archivo no existe, creando lista vacía");
                    _users = new List<UserCredentials>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error al cargar usuarios: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                _users = new List<UserCredentials>();
            }
        }

        /// <summary>
        /// Guarda los usuarios en el archivo
        /// </summary>
        private void SaveUsers()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Guardando {_users.Count} usuarios en {_usersFilePath}");

                var json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_usersFilePath, json);

                System.Diagnostics.Debug.WriteLine($"[DEBUG] Archivo guardado exitosamente");

                // Verificar que el archivo se guardó correctamente releyéndolo
                if (File.Exists(_usersFilePath))
                {
                    var verification = File.ReadAllText(_usersFilePath);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Verificación: archivo tiene {verification.Length} caracteres");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error al guardar usuarios: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Registra un nuevo usuario
        /// </summary>
        public (bool success, string message) RegisterUser(string email, string password, string fullName, string turno)
        {
            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(email))
                return (false, "El email no puede estar vacío");

            if (string.IsNullOrWhiteSpace(password))
                return (false, "La contraseña no puede estar vacía");

            if (string.IsNullOrWhiteSpace(fullName))
                return (false, "El nombre no puede estar vacío");

            if (string.IsNullOrWhiteSpace(turno))
                return (false, "Por favor selecciona un turno");

            if (password.Length < 6)
                return (false, "La contraseña debe tener al menos 6 caracteres");

            // Verificar si el email ya existe
            if (_users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                return (false, "Este email ya está registrado");

            // Crear nuevo usuario
            var newUser = new UserCredentials
            {
                Email = email.ToLower(),
                Password = password,
                FullName = fullName,
                Turno = turno
            };

            _users.Add(newUser);
            SaveUsers();

            return (true, "Usuario registrado exitosamente");
        }

        /// <summary>
        /// Valida las credenciales de un usuario
        /// </summary>
        public (bool success, string message) ValidateUser(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
                return (false, "El email no puede estar vacío");

            if (string.IsNullOrWhiteSpace(password))
                return (false, "La contraseña no puede estar vacía");

            var user = _users.FirstOrDefault(u => 
                u.Email.Equals(email.ToLower(), StringComparison.OrdinalIgnoreCase));

            if (user == null)
                return (false, "Email o contraseña incorrectos");

            if (!user.Password.Equals(password))
                return (false, "Email o contraseña incorrectos");

            return (true, $"Bienvenido, {user.FullName}");
        }

        /// <summary>
        /// Obtiene los datos de un usuario
        /// </summary>
        public UserCredentials GetUser(string email)
        {
            return _users.FirstOrDefault(u => 
                u.Email.Equals(email.ToLower(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Obtiene el nombre completo del usuario logueado
        /// </summary>
        public string GetFullName(string email)
        {
            var user = GetUser(email);
            return user?.FullName ?? "Usuario";
        }

        /// <summary>
        /// Actualiza los datos de un usuario
        /// </summary>
        public (bool success, string message) UpdateUser(string currentEmail, string newEmail, string fullName, string password, string turno)
        {
            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(newEmail))
                return (false, "El email no puede estar vacío");

            if (string.IsNullOrWhiteSpace(fullName))
                return (false, "El nombre no puede estar vacío");

            if (string.IsNullOrWhiteSpace(password))
                return (false, "La contraseña no puede estar vacía");

            if (string.IsNullOrWhiteSpace(turno))
                return (false, "Por favor selecciona un turno");

            if (password.Length < 6)
                return (false, "La contraseña debe tener al menos 6 caracteres");

            try
            {
                // Recargar usuarios desde archivo para asegurar que tenemos datos frescos
                LoadUsers();

                // Buscar usuario actual
                var user = _users.FirstOrDefault(u => 
                    u.Email.Equals(currentEmail.ToLower(), StringComparison.OrdinalIgnoreCase));

                if (user == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Usuario no encontrado: {currentEmail}");
                    return (false, "Usuario no encontrado");
                }

                // Verificar si el nuevo email ya existe (y no es el mismo usuario)
                if (!currentEmail.Equals(newEmail, StringComparison.OrdinalIgnoreCase))
                {
                    if (_users.Any(u => u.Email.Equals(newEmail.ToLower(), StringComparison.OrdinalIgnoreCase)))
                        return (false, "Este email ya está registrado");
                }

                // Actualizar datos
                user.Email = newEmail.ToLower();
                user.FullName = fullName;
                user.Password = password;
                user.Turno = turno;

                System.Diagnostics.Debug.WriteLine($"[DEBUG] Actualizando usuario - Email: {user.Email}, Nombre: {user.FullName}, Turno: {user.Turno}");

                SaveUsers();

                System.Diagnostics.Debug.WriteLine($"[DEBUG] Usuario actualizado exitosamente");

                return (true, "Datos actualizados exitosamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error al actualizar usuario: {ex.Message}");
                return (false, $"Error al actualizar: {ex.Message}");
            }
        }
    }
}
