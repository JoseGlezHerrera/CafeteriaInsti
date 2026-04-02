using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Resubscribe();
        _vm.PropertyChanged += OnVmPropertyChanged;
        if (_vm.IsLoading) StartSkeletonAnimation();
    }

    // FIX-12: Desuscribir al desaparecer para evitar memory leaks
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.Cleanup();
        this.AbortAnimation("skeleton");
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.IsLoading))
        {
            if (_vm.IsLoading) StartSkeletonAnimation();
            else this.AbortAnimation("skeleton");
        }
    }

    private void StartSkeletonAnimation()
    {
        this.AbortAnimation("skeleton");
        var anim = new Animation(v => SkeletonGrid.Opacity = v, 0.35, 1.0);
        anim.Commit(this, "skeleton", length: 900, easing: Easing.SinInOut, repeat: () => true);
    }
}
