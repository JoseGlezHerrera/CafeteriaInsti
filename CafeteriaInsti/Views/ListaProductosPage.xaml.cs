// Views/ListaProductosPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class ListaProductosPage : ContentPage
    {
        public ListaProductosPage()
        {
            System.Diagnostics.Debug.WriteLine("[P핯INA] ===== ListaProductosPage Constructor =====");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[P핯INA] InitializeComponent completado");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            System.Diagnostics.Debug.WriteLine("[P핯INA] ===== OnAppearing INICIO =====");
            
            // Obtener el ViewModel desde DI si no existe
            if (BindingContext == null)
            {
                System.Diagnostics.Debug.WriteLine("[P핯INA] BindingContext es NULL, obteniendo desde DI...");
                
                var mauiContext = App.Current?.Handler?.MauiContext;
                System.Diagnostics.Debug.WriteLine($"[P핯INA] MauiContext es null? {mauiContext == null}");
                
                var viewModel = mauiContext?.Services.GetService<ListaProductosViewModel>();
                System.Diagnostics.Debug.WriteLine($"[P핯INA] ViewModel obtenido: {viewModel != null}");
                
                if (viewModel != null)
                {
                    BindingContext = viewModel;
                    System.Diagnostics.Debug.WriteLine($"[P핯INA] BindingContext establecido. Productos en ViewModel: {viewModel.Productos?.Count ?? 0}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[P핯INA] ERROR: No se pudo obtener ViewModel desde DI!");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[P핯INA] BindingContext ya existe: {BindingContext.GetType().Name}");
                if (BindingContext is ListaProductosViewModel vm)
                {
                    System.Diagnostics.Debug.WriteLine($"[P핯INA] Productos en ViewModel existente: {vm.Productos?.Count ?? 0}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine("[P핯INA] ===== OnAppearing FIN =====");
        }
    }
}


