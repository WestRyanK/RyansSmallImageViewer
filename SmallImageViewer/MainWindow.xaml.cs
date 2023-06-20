using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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

		private AppViewModel? AppViewModel => (Application.Current as App)?.AppViewModel;

		public MainWindow() {
			InitializeComponent();

			if (AppViewModel is AppViewModel app) {
				app.PropertyChanged += App_PropertyChanged;
				ViewModel = new WindowViewModel(Properties.Settings.Default.FolderPath, app);
			}
			else {
				Debug.WriteLine("Null AppViewModel");
			}
		}

		private void App_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == nameof(AppViewModel.ImageSize)) {
				if (ImageGrid.SelectedItem != null) {
					ImageGrid.ScrollIntoView(ImageGrid.SelectedItem);
				}
			}
		}

		protected override void OnClosed(EventArgs e) {
			base.OnClosed(e);
			Properties.Settings.Default.FolderPath = ViewModel?.FolderPath;
			Properties.Settings.Default.Save();
		}

		private void ListView_MouseMove(object sender, MouseEventArgs e) {
			if (e.LeftButton != MouseButtonState.Pressed) {
				return;
			}
			if (!(sender is Selector selectorControl)) {
				return;
			}

			var item = selectorControl.SelectedItem;
			// Ensure we're dragging the selected item and not something like the scrollbar.
			selectorControl.UpdateLayout();
			var itemContainer = selectorControl.ItemContainerGenerator.ContainerFromItem(item) as Visual;
			if (item == null || itemContainer == null || !IsVisualAncestor(e.OriginalSource as Visual, itemContainer)) {
				return;
			}
			if (!(item is ImageItem imageItem)) {
				return;
			}

			DataObject dataObject = new DataObject(DataFormats.FileDrop, new string[] { imageItem.Filename });
			DragDrop.DoDragDrop(selectorControl, dataObject, DragDropEffects.Copy);
		}

		private static bool IsVisualAncestor(Visual? element, Visual ancestor) {
			while (element != null) {
				if (element == ancestor) {
					return true;
				}
				element = VisualTreeHelper.GetParent(element) as Visual;
			}
			return false;
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

		public AppViewModel? AppViewModel {
			get;
			private set;
		}

		private string? _folderPath;
		public string? FolderPath {
			get => _folderPath;
			set {
				if (SetProperty(ref _folderPath, value) && !_isDesignMode) {
					LoadImageItems();
					WatchFolder();
				}
			}
		}

		private FileSystemWatcher? _watcher;
		private RapidWaiter _reloadWaiter;
		private bool _isDesignMode = false;

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

		public WindowViewModel() {
			_isDesignMode = System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject());
			if (_isDesignMode) {
				FolderPath = @"C:\Test\Path";
				_reloadWaiter = new RapidWaiter();
			}
		}

		public WindowViewModel(string folderPath, AppViewModel? appViewModel) {
			FolderPath = folderPath;
			AppViewModel = appViewModel;

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
				MainWindow mainWindow = new MainWindow();
				mainWindow.Show();
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
