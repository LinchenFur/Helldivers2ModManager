using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Helldivers2ModManager.Components;
using Helldivers2ModManager.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;

namespace Helldivers2ModManager.ViewModels;

internal sealed partial class SettingsPageViewModel : PageViewModelBase
{
	public override string Title => "Settings";

	public string GameDir
	{
		get => _settingsStore.GameDirectory;
		set
		{
			OnPropertyChanging();
			_settingsStore.GameDirectory = value;
			OnPropertyChanged();
		}
	}

	public string TempDir
	{
		get => _settingsStore.TempDirectory;
		set
		{
			OnPropertyChanging();
			_settingsStore.TempDirectory = value;
			OnPropertyChanged();
		}
	}

	public string StorageDir
	{
		get => _settingsStore.StorageDirectory;
		set
		{
			OnPropertyChanging();
			_settingsStore.StorageDirectory = value;
			OnPropertyChanged();

			_storageDirChanged = true;
			WeakReferenceMessenger.Default.Send(new MessageBoxInfoMessage()
			{
				Message = "Storage directory changed. The application needs to be restarted and will quit once you hit \"OK\"."
			});
		}
	}

	public LogLevel LogLevel
	{
		get => _settingsStore.LogLevel;
		set
		{
			OnPropertyChanging();
			_settingsStore.LogLevel = value;
			OnPropertyChanged();
		}
	}

	public float Opacity
	{
		get => _settingsStore.Opacity;
		set
		{
			OnPropertyChanging();
			_settingsStore.Opacity = value;
			OnPropertyChanged();
		}
	}

	public ObservableCollection<string> SkipList => _settingsStore.SkipList;

	public bool CaseSensitiveSearch
	{
		get => _settingsStore.CaseSensitiveSearch;
		set
		{
			OnPropertyChanging();
			_settingsStore.CaseSensitiveSearch = value;
			OnPropertyChanged();
		}
	}

	private readonly ILogger<SettingsPageViewModel> _logger;
	private readonly NavigationStore _navStore;
	private readonly SettingsStore _settingsStore;
	private bool _storageDirChanged = false;
	[ObservableProperty]
	private int _selectedSkip = -1;

	public SettingsPageViewModel(ILogger<SettingsPageViewModel> logger, NavigationStore navStore, SettingsStore settingsStore)
	{
		_logger = logger;
		_navStore = navStore;
		_settingsStore = settingsStore;

		SkipList.CollectionChanged += SkipList_CollectionChanged;
	}

	private static bool ValidateGameDir(DirectoryInfo dir, [NotNullWhen(false)] out string? error)
	{
		if (!dir.Exists)
		{
			error = "The selected Helldivers 2 folder does not exist!";
			return false;
		}

		if (dir is not DirectoryInfo { Name: "Helldivers 2" })
		{
			error = "The selected Helldivers 2 folder does not reside in a valid directory!";
			return false;
		}

		var subDirs = dir.EnumerateDirectories();
		if (!subDirs.Any(static d => d.Name == "data"))
		{
			error = "The selected Helldivers 2 root path does not contain a directory named \"data\"!";
			return false;
		}
		if (!subDirs.Any(static d => d.Name == "tools"))
		{
			error = "The selected Helldivers 2 root path does not contain a directory named \"tools\"!";
			return false;
		}
		if (subDirs.FirstOrDefault(static d => d.Name == "bin") is not DirectoryInfo binDir)
		{
			error = "The selected Helldivers 2 root path does not contain a directory named \"bin\"!";
			return false;
		}
		if (!binDir.GetFiles("helldivers2.exe").Any())
		{
			error = "The selected Helldivers 2 path does not contain a file named \"helldivers2.exe\" in a folder called \"bin\"!";
			return false;
		}

		error = null;
		return true;
	}

	private bool ValidateSettings()
	{
		if (string.IsNullOrEmpty(GameDir))
		{
			WeakReferenceMessenger.Default.Send(new MessageBoxErrorMessage()
			{
				Message = "游戏目录不能留空！"
			});
			return false;
		}

		if (string.IsNullOrEmpty(StorageDir))
		{
			WeakReferenceMessenger.Default.Send(new MessageBoxErrorMessage()
			{
				Message = "存储目录不能留空！"
			});
			return false;
		}

		if (string.IsNullOrEmpty(TempDir))
		{
			WeakReferenceMessenger.Default.Send(new MessageBoxErrorMessage()
			{
				Message = "临时目录不能留空！"
			});
			return false;
		}

		return true;
	}

	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(SelectedSkip))
			RemoveSkipCommand.NotifyCanExecuteChanged();

		base.OnPropertyChanged(e);
	}

	private void SkipList_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		RemoveSkipCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand]
	void Ok()
	{
		if (!ValidateSettings())
			return;

		_settingsStore.Save();

		if (_storageDirChanged)
			Application.Current.Shutdown();
		else
			_navStore.Navigate<DashboardPageViewModel>();
	}

	[RelayCommand]
	void Reset()
	{
		WeakReferenceMessenger.Default.Send(new MessageBoxConfirmMessage
		{
			Title = "重置?",
			Message = "您真的要重置您的设置吗？",
			Confirm = () =>
			{
				_settingsStore.Reset();
				OnPropertyChanged(nameof(GameDir));
				OnPropertyChanged(nameof(TempDir));
				OnPropertyChanged(nameof(StorageDir));
				OnPropertyChanged(nameof(LogLevel));
				OnPropertyChanged(nameof(Opacity));
			}
		});
	}

	[RelayCommand]
	void BrowseGame()
	{
		var dialog = new OpenFolderDialog
		{
			Multiselect = false,
			Title = "请选择你的游戏目录"
		};

		if (dialog.ShowDialog() ?? false)
		{
			var newDir = new DirectoryInfo(dialog.FolderName);

			if (newDir.Parent is DirectoryInfo { Name: "Helldivers 2" })
			{
				newDir = newDir.Parent;
			}

			if (ValidateGameDir(newDir, out var error))
				GameDir = newDir.FullName;
			else
				WeakReferenceMessenger.Default.Send(new MessageBoxErrorMessage
				{
					Message = error
				});
		}
	}

	[RelayCommand]
	void BrowseStorage()
	{
		var dialog = new OpenFolderDialog
		{
			Multiselect = false,
			ValidateNames = true,
			Title = "请选择一个你希望这个管理器存储其模块的文件夹..."
		};

		if (dialog.ShowDialog() ?? false)
			StorageDir = dialog.FolderName;
	}

	[RelayCommand]
	void BrowseTemp()
	{
		var dialog = new OpenFolderDialog
		{
			Multiselect = false,
			ValidateNames = true,
			Title = "请选择此管理器的临时文件夹..."
		};

		if (dialog.ShowDialog() ?? false)
			TempDir = dialog.FolderName;
	}

	[RelayCommand]
	void HardPurge()
	{
		_logger.LogInformation("Hard purging patch files");
		
		var path = Path.Combine(_settingsStore.StorageDirectory, "installed.txt");
		if (File.Exists(path))
			File.Delete(path);

		var dataDir = new DirectoryInfo(Path.Combine(_settingsStore.GameDirectory, "data"));
		
		var files = dataDir.EnumerateFiles("*.patch_*").ToArray();
		_logger.LogDebug("Found {} patch files", files.Length);

		foreach (var file in files)
		{
			_logger.LogTrace("Deleting \"{}\"", file.Name);
			file.Delete();
		}

		_logger.LogInformation("Hard purge complete");
	}

	[RelayCommand]
	void AddSkip()
	{
		WeakReferenceMessenger.Default.Send(new MessageBoxInputMessage
		{
			Title = "File name?",
			Message = "请输入要跳过补丁 0 的存档文件的 16 个字符的名称",
			MaxLength = 16,
			Confirm = (str) =>
			{
				if (str.Length == 16)
					SkipList.Add(str);
				else
					WeakReferenceMessenger.Default.Send(new MessageBoxInfoMessage
					{
						Message = "Archive file names can only be 16 characters long."
					});
			}
		});
	}

	bool CanRemoveSkip()
	{
		return SelectedSkip != -1;
	}

	[RelayCommand(CanExecute = nameof(CanRemoveSkip))]
	void RemoveSkip()
	{
		SkipList.RemoveAt(SelectedSkip);
	}

	[RelayCommand]
	async Task DetectGame()
	{
		WeakReferenceMessenger.Default.Send(new MessageBoxProgressMessage
		{
			Title = "寻找游戏",
			Message = "请民主的等待"
		});

		var (result, path) = await Task.Run<(bool, string?)>(static () =>
		{
			foreach(var drive in Environment.GetLogicalDrives())
			{
				string path;
				if (drive == "C:\\")
				{
					path = Path.Combine(drive, "Program Files (x86)", "Steam", "steamapps", "common", "Helldivers 2");
					if (ValidateGameDir(new DirectoryInfo(path), out _))
						return (true, path);
				}

				path = Path.Combine(drive, "Steam", "steamapps", "common", "Helldivers 2");
				if (ValidateGameDir(new DirectoryInfo(path), out _))
					return (true, path);

				path = Path.Combine(drive, "SteamLibrary", "steamapps", "common", "Helldivers 2");
				if (ValidateGameDir(new DirectoryInfo(path), out _))
					return (true, path);
			}

			return (false, null);
		});

		if (result)
			WeakReferenceMessenger.Default.Send(new MessageBoxHideMessage());
		else
			WeakReferenceMessenger.Default.Send(new MessageBoxInfoMessage
			{
				Message = "“无法自动找到您的 Helldivers 2 游戏.请手动设置"
			});
	}
}
