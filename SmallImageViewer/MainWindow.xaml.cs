using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmallImageViewer {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		public WindowViewModel? ViewModel {
			get => DataContext as WindowViewModel;
			set => DataContext = value;
		}

		public MainWindow() {
			InitializeComponent();
			ViewModel = new WindowViewModel(Properties.Settings.Default.FolderPath, Properties.Settings.Default.ImageSize);
		}

		protected override void OnClosed(EventArgs e) {
			base.OnClosed(e);
			Properties.Settings.Default.FolderPath = ViewModel?.FolderPath;
			Properties.Settings.Default.ImageSize = ViewModel?.ImageSize ?? WindowViewModel.DefaultImageSize;
			Properties.Settings.Default.Save();
		}
	}

	public class ImageItem {
		public string Filename { get; set; }
		public string Name { get; set; }
		public BitmapImage Image { get; set; }

		public ImageItem(string filename) {
			Filename = filename;
			Name = System.IO.Path.GetFileName(filename);

			Image = LoadImage(filename);
		}

		private static BitmapImage LoadImage(string filename) {
			using var stream = new FileStream(filename, FileMode.Open);
			var image = new BitmapImage();
			image.BeginInit();
			image.StreamSource = stream;
			image.CacheOption = BitmapCacheOption.OnLoad;
			image.EndInit();
			image.Freeze();
			return image;
		}
	}

	public class WindowViewModel : ViewModelBase {
		public ObservableCollection<ImageItem> ImageItems { get; set; } = new ObservableCollection<ImageItem>();

		private string? _folderPath;
		public string? FolderPath {
			get => _folderPath;
			set {
				if (SetProperty(ref _folderPath, value)) {
					LoadImageItems();
					WatchFolder();
				}
			}
		}

		public const float DefaultImageSize = 200;
		private float _imageSize = DefaultImageSize;
		public float ImageSize {
			get => _imageSize;
			set => SetProperty(ref _imageSize, value);
		}

		private FileSystemWatcher? _watcher;
		private RapidWaiter _reloadWaiter;

		private void WatchFolder() {
			_watcher?.Dispose();
			_watcher = null;
			if (FolderPath != null) {
				_watcher = new FileSystemWatcher(FolderPath);
				_watcher.EnableRaisingEvents = true;
				_watcher.Changed += Watcher_Fired;
				_watcher.Created += Watcher_Fired;
				_watcher.Deleted += Watcher_Fired;
			}
		}

		public WindowViewModel(string folderPath, float imageSize) {
			FolderPath = folderPath;
			ImageSize = imageSize;

			_reloadWaiter = new RapidWaiter() {
				Delay = TimeSpan.FromSeconds(1),
				RapidAction = () => {
					LoadImageItems();
				}
			};

			LoadImageItems();
			WatchFolder();
		}

		private void Watcher_Fired(object sender, FileSystemEventArgs e) {
			_reloadWaiter.Fire();
		}

		private void LoadImageItems() {
			ImageItems.Clear();
			GetAllPngFiles(FolderPath)
				.ForEach(p => ImageItems.Add(new ImageItem(p)));
		}

		public ICommand SelectFolderCommand => new Command {
			ExecuteAction = _ => {

				System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
				if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
					FolderPath = dialog.SelectedPath;
				}
			}
		};

		public ICommand ViewFolderCommand => new Command {
			CanExecuteAction = _ => !string.IsNullOrEmpty(FolderPath),
			ExecuteAction = _ => {
				if (FolderPath != null) {
					System.Diagnostics.Process.Start("explorer.exe", FolderPath);
				}
			}
		};

		public ICommand NewWindowCommand => new Command {
			ExecuteAction = _ => {
					System.Diagnostics.Process.Start(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
			}
		};

		private IEnumerable<string> GetAllPngFiles(string? folderPath) {
			if (folderPath == null) {
				return Enumerable.Empty<string>();
			}

			var filePaths = Directory.GetFiles(folderPath);
			return filePaths
				.Where(p => System.IO.Path.GetExtension(p) == ".png")
				.OrderBy(p => System.IO.Path.GetFileName(p));
		}

		public ICommand ClearFolderCommand => new Command {
			ExecuteAction = _ => {
				GetAllPngFiles(FolderPath)
					.ForEach(p => File.Delete(p));
			}
		};
	}

	public class ViewModelBase : INotifyPropertyChanged {
		public event PropertyChangedEventHandler? PropertyChanged;

		protected bool SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = "") {
			if (!Equals(property, value)) {
				property = value;
				OnPropertyChanged(propertyName);
				return true;
			}
			return false;
		}

		protected void OnPropertyChanged(string propertyName) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public class Command : ICommand {
		public Action<object?> ExecuteAction { get; set; }
		public Func<object?, bool> CanExecuteAction { get; set; }

		public event EventHandler? CanExecuteChanged;

		public bool CanExecute(object? parameter) {
			return CanExecuteAction?.Invoke(parameter) ?? true;
		}

		public void Execute(object? parameter) {
			ExecuteAction?.Invoke(parameter);
		}
	}

	public static class Extensions {
		public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action) {
			foreach (var item in enumerable) {
				action(item);
			}
		}
	}

	public class RapidWaiter {
		public Action RapidAction { get; set; }
		public TimeSpan Delay { get; set; }

		private DispatcherTimer _timer;

		public RapidWaiter() {
			_timer = new DispatcherTimer();
			_timer.Tick += _timer_Tick;
			_timer.IsEnabled = false;
		}

		private void _timer_Tick(object? sender, EventArgs e) {
			_timer.IsEnabled = false;
			RapidAction?.Invoke();
		}

		public void Fire() {
			_timer.Stop();
			_timer.Interval = Delay;
			_timer.Start();
		}
	}
}
