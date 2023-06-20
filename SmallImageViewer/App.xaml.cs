using System.Windows;

namespace SmallImageViewer {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		public AppViewModel AppViewModel {
			get;
			set;
		}

		public App() {
			AppViewModel = new AppViewModel();
			var settings = SmallImageViewer.Properties.Settings.Default;
			AppViewModel.ImageSize = settings.ImageSize;
			AppViewModel.WindowWidth = settings.WindowWidth;
			AppViewModel.WindowHeight = settings.WindowHeight;
			AppViewModel.IsWindowOnTop = settings.IsWindowOnTop;
		}

		protected override void OnExit(ExitEventArgs e) {
			SmallImageViewer.Properties.Settings.Default.ImageSize = AppViewModel?.ImageSize ?? AppViewModel.DefaultImageSize;
			SmallImageViewer.Properties.Settings.Default.WindowWidth = AppViewModel?.WindowWidth ?? AppViewModel.DefaultWindowWidth;
			SmallImageViewer.Properties.Settings.Default.WindowHeight = AppViewModel?.WindowHeight ?? AppViewModel.DefaultWindowHeight;
			SmallImageViewer.Properties.Settings.Default.IsWindowOnTop = AppViewModel?.IsWindowOnTop ?? false;
			SmallImageViewer.Properties.Settings.Default.Save();
			base.OnExit(e);
		}
	}

	public class AppViewModel : ViewModelBase {
		public const float DefaultImageSize = 200;
		private float _imageSize = DefaultImageSize;
		public float ImageSize {
			get => _imageSize;
			set => SetProperty(ref _imageSize, value);
		}

		public static readonly double DefaultWindowWidth = 400;
		private double _windowWidth = DefaultWindowWidth;
		public double WindowWidth {
			get => _windowWidth;
			set => SetProperty(ref _windowWidth, value);
		}

		public static readonly double DefaultWindowHeight = 400;
		private double _windowHeight = DefaultWindowHeight;
		public double WindowHeight {
			get => _windowHeight;
			set => SetProperty(ref _windowHeight, value);
		}

		private bool _isWindowOnTop = false;
		public bool IsWindowOnTop {
			get => _isWindowOnTop;
			set => SetProperty(ref _isWindowOnTop, value);
		}
	}
}
