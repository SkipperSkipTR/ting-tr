using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TingTR_Installer
{
    public partial class MainWindow : Window
    {
        private static readonly string MelonLoaderVersion = "v0.7.2";
        private static readonly string MelonLoaderUrl = $"https://github.com/LavaGang/MelonLoader/releases/download/{MelonLoaderVersion}/MelonLoader.x64.zip";
        private static readonly string TingTrModUrl = "https://github.com/SkipperSkipTR/ting-tr/releases/download/v1.0/Mods.rar";
        private const string ExpectedGameAssemblyHash = "56C38134859A64872ED3EF523110695F16D9B8D9E24629207CB67FE31C33531D";

        private string _gamePath = null;
        private string _backupPath = null;
        private HttpClient _httpClient = new HttpClient();
        private bool _isInstalling = false;

        public MainWindow()
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    Exception ex = (Exception)e.ExceptionObject;
                    MessageBox.Show($"Kritik hata: {ex.Message}\n\n{ex.StackTrace}",
                        "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                };

                InitializeComponent();
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                _httpClient.Timeout = TimeSpan.FromMinutes(5);
                Dispatcher.Invoke(Initialize);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Başlangıç hatası:\n\n{ex.Message}\n\nDetay:\n{ex.StackTrace}",
                    "Başlangıç Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Initialize()
        {
            UpdateStatus("Başlıyor...");
            SetButtonsEnabled(false);

            try
            {
                UpdateStatus("Steam klasörü aranıyor...");
                await Task.Delay(100);

                string detectedPath = SteamHelper.FindGamePath();
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    _gamePath = detectedPath;
                    GamePathText.Text = $"Oyun konumu: {TruncatePath(_gamePath, 60)}";
                    UpdateStatus("GameAssembly.dll versiyonu kontrol ediliyor...");
                    await Task.Delay(50);

                    if (!CheckGameAssemblyVersion())
                    {
                        SelectFolderBtn.Visibility = Visibility.Visible;
                        InstallBtn.IsEnabled = false;
                        return;
                    }

                    UpdateStatus("Oyun bulundu! Kurulum için 'Kur' butonuna basın.");

                    bool hasMelonLoader = Directory.Exists(Path.Combine(_gamePath, "MelonLoader"));
                    bool hasMods = File.Exists(Path.Combine(_gamePath, "Mods", "Ting_Tr.dll"));

                  if (hasMelonLoader && hasMods)
                        {
                            UpdateStatus("MelonLoader ve yama zaten yüklü.");
                            InstallBtn.Visibility = Visibility.Collapsed;
                            UninstallBtn.Visibility = Visibility.Visible;
                        }
                        else if (hasMelonLoader)
                        {
                            UpdateStatus("MelonLoader zaten yüklü, ancak yama eksik.");
                            InstallBtn.Visibility = Visibility.Visible;
                            UninstallBtn.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            InstallBtn.Visibility = Visibility.Visible;
                            UninstallBtn.Visibility = Visibility.Collapsed;
                        }
                }
                else
                {
                    GamePathText.Text = "Oyun otomatik bulunamadı";
                    UpdateStatus("Oyun otomatik tespit edilemedi. Klasörü manuel seçin.");
                    SelectFolderBtn.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Hata: {ex.Message}");
                GamePathText.Text = "Hata oluştu";
                SelectFolderBtn.Visibility = Visibility.Visible;
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private void SelectFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Oyun klasörünü seçin\n(There is no game - Wrong dimension klasörü)"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _gamePath = dialog.SelectedPath;
                GamePathText.Text = $"Oyun konumu: {TruncatePath(_gamePath, 60)}";

                bool hasExe = Directory.GetFiles(_gamePath, "*.exe").Length > 0;
                if (!hasExe)
                {
                    MessageBox.Show(
                        "Seçilen klasörde .exe dosyası bulunamadı!\n" +
                        "Lütfen doğru klasörü seçin.",
                        "Hata",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    _gamePath = null;
                    GamePathText.Text = "Oyun konumu: Tespit edilemedi";
                    return;
                }

                UpdateStatus("Oyun klasörü seçildi. Kurulum için 'Kur' butonuna basın.");
                SelectFolderBtn.Visibility = Visibility.Collapsed;
            }
        }

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isInstalling || string.IsNullOrEmpty(_gamePath))
                return;

            _isInstalling = true;
            SetButtonsEnabled(false);
            InstallBtn.IsEnabled = false;

               try
            {
                if (!CheckGameAssemblyVersion())
                {
                    return;
                }

                bool hasMelonLoader = Directory.Exists(Path.Combine(_gamePath, "MelonLoader"));
                bool hasMods = Directory.Exists(Path.Combine(_gamePath, "Mods"));

                if (!hasMelonLoader)
                {
                    await InstallMelonLoader();
                }
                else
                {
                    UpdateStatus("MelonLoader zaten yüklü, atlanıyor...");
                }

                if (!hasMods)
                {
                    await InstallMod();
                }
                else
                {
                    UpdateStatus("Mod klasörü zaten var, atlanıyor...");
                }

                UpdateStatus("Kurulum başarıyla tamamlandı!");
                MessageBox.Show(
                    "Kurulum başarıyla tamamlandı!\n\n" +
                    "Oyunu çalıştırabilirsiniz.",
                    "Kurulum Tamamlandı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                InstallBtn.Visibility = Visibility.Collapsed;
                UninstallBtn.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                UpdateStatus($"HATA: {ex.Message}");
                MessageBox.Show(
                    $"Kurulum sırasında hata oluştu:\n\n{ex.Message}\n\n" +
                    $"Detay: {ex.InnerException?.Message ?? "Yok"}",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isInstalling = false;
                SetButtonsEnabled(true);
                InstallBtn.IsEnabled = true;
            }
        }

        private async Task InstallMelonLoader()
        {
            UpdateStatus("MelonLoader indiriliyor...");
            InstallProgress.Value = 0;
            ProgressText.Text = "MelonLoader indiriliyor...";

            string tempPath = Path.Combine(Path.GetTempPath(), "MelonLoader.zip");

            try
            {
                await DownloadFileWithProgress(MelonLoaderUrl, tempPath, (progress) =>
                {
                    InstallProgress.Value = progress;
                    ProgressText.Text = $"MelonLoader: %{progress}";
                });

                UpdateStatus("MelonLoader açılıyor...");
                ProgressText.Text = "MelonLoader açılıyor...";
                InstallProgress.Value = 50;

                Directory.CreateDirectory(_gamePath);
                ArchiveHelper.ExtractArchive(tempPath, _gamePath);

                File.Delete(tempPath);

                InstallProgress.Value = 100;
                ProgressText.Text = "MelonLoader yüklendi!";
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        private async Task InstallMod()
        {
            UpdateStatus("TING yaması indiriliyor...");
            InstallProgress.Value = 0;
            ProgressText.Text = "Yama indiriliyor...";

            string tempPath = Path.Combine(Path.GetTempPath(), "TING_Mods.rar");

            try
            {
                await DownloadFileWithProgress(TingTrModUrl, tempPath, (progress) =>
                {
                    InstallProgress.Value = progress;
                    ProgressText.Text = $"Yama: %{progress}";
                });

                UpdateStatus("Yama açılıyor...");
                ProgressText.Text = "Yama açılıyor...";
                InstallProgress.Value = 50;

                string localizationPath = Path.Combine(_gamePath, "Ting_Data", "StreamingAssets", "Localization.csv");
                if (File.Exists(localizationPath))
                {
                    string backupDir = Path.Combine(_gamePath, "TingTR_Backup");
                    Directory.CreateDirectory(backupDir);
                    _backupPath = Path.Combine(backupDir, "Localization.csv");
                    File.Copy(localizationPath, _backupPath, true);
                    UpdateStatus("Orijinal Localization.csv yedeklendi.");
                }

                string modsPath = Path.Combine(_gamePath, "Mods");
                Directory.CreateDirectory(modsPath);
                ArchiveHelper.ExtractArchive(tempPath, modsPath);

                File.Delete(tempPath);

                InstallProgress.Value = 100;
                ProgressText.Text = "Yama yüklendi!";
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        private void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_gamePath))
                return;

            MessageBoxResult result = MessageBox.Show(
                "MelonLoader ve TING yamasi kaldırılacak.\nDevam etmek istiyor musunuz?",
                "Kaldırma Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Uninstall();
            }
        }

        private void Uninstall()
        {
            try
            {
                bool removed = false;

                string melonLoaderPath = Path.Combine(_gamePath, "MelonLoader");
                if (Directory.Exists(melonLoaderPath))
                {
                    Directory.Delete(melonLoaderPath, true);
                    UpdateStatus("MelonLoader kaldırıldı.");
                    removed = true;
                }

                string userDataPath = Path.Combine(_gamePath, "UserData");
                if (Directory.Exists(userDataPath))
                {
                    Directory.Delete(userDataPath, true);
                    UpdateStatus("UserData kaldırıldı.");
                    removed = true;
                }

                string userLibsPath = Path.Combine(_gamePath, "UserLibs");
                if (Directory.Exists(userLibsPath))
                {
                    Directory.Delete(userLibsPath, true);
                    UpdateStatus("UserLibs kaldırıldı.");
                    removed = true;
                }

                string pluginsPath = Path.Combine(_gamePath, "Plugins");
                if (Directory.Exists(pluginsPath))
                {
                    Directory.Delete(pluginsPath, true);
                    UpdateStatus("Plugins kaldırıldı.");
                    removed = true;
                }

                string modsPath = Path.Combine(_gamePath, "Mods");
                if (Directory.Exists(modsPath))
                {
                    Directory.Delete(modsPath, true);
                    UpdateStatus("TING yaması kaldırıldı.");
                    removed = true;
                }

                string backupPath = Path.Combine(_gamePath, "TingTR_Backup", "Localization.csv");
                if (File.Exists(backupPath))
                {
                    string localizationPath = Path.Combine(_gamePath, "Ting_Data", "StreamingAssets", "Localization.csv");
                    File.Copy(backupPath, localizationPath, true);
                    UpdateStatus("Localization.csv geri yüklendi.");
                    removed = true;

                    string backupDir = Path.Combine(_gamePath, "TingTR_Backup");
                    Directory.Delete(backupDir, true);
                }

                string versionDLLPath = Path.Combine(_gamePath, "version.dll");
                if (File.Exists(versionDLLPath))
                {
                    File.Delete(versionDLLPath);
                    UpdateStatus("version.dll kaldırıldı.");
                    removed = true;
                }

                if (removed)
                {
                    UpdateStatus("Kaldırma başarıyla tamamlandı.");
                    MessageBox.Show(
                        "Kaldırma işlemi tamamlandı!",
                        "Tamamlandı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    InstallBtn.Visibility = Visibility.Visible;
                    UninstallBtn.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UpdateStatus("Kaldırılacak dosya bulunamadı.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Kaldırma hatası: {ex.Message}");
                MessageBox.Show(
                    $"Kaldırma sırasında hata:\n\n{ex.Message}",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task DownloadFileWithProgress(string url, string filePath, Action<int> progressCallback)
        {
            UpdateStatus($"İndiriliyor: {url}");

            using HttpResponseMessage response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? 0;
            long downloadedBytes = 0;

            using Stream contentStream = await response.Content.ReadAsStreamAsync();
            using FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    int progress = (int)((downloadedBytes * 100) / totalBytes);
                    progressCallback(progress);
                }
                else
                {
                    progressCallback(0);
                }
            }

            progressCallback(100);
        }

        private string TruncatePath(string path, int maxLength)
        {
            if (path.Length <= maxLength)
                return path;

            int folderPart = path.LastIndexOf('\\');
            if (folderPart > 0)
            {
                string folderName = path.Substring(folderPart + 1);
                if (folderName.Length + 4 <= maxLength)
                {
                    return $"...\\{folderName}";
                }
            }
            return $"...{path.Substring(path.Length - (maxLength - 3))}";
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }

        private void SetButtonsEnabled(bool enabled)
        {
            InstallBtn.IsEnabled = enabled;
            UninstallBtn.IsEnabled = enabled;
            SelectFolderBtn.IsEnabled = enabled;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _httpClient?.Dispose();
            base.OnClosing(e);
        }

        private static string GetFileHash(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                using (FileStream stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                }
            }
        }

        private bool CheckGameAssemblyVersion()
        {
            string dllPath = Path.Combine(_gamePath, "GameAssembly.dll");
            if (!File.Exists(dllPath))
            {
                UpdateStatus("GameAssembly.dll dosyası bulunamadı!");
                GamePathText.Text = "GameAssembly.dll dosyası eksik";
                MessageBox.Show(
                    "GameAssembly.dll dosyası oyun klasöründe bulunamadı!\n\n" +
                    "Lütfen doğru oyun klasörünü seçin.",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            try
            {
                string actualHash = GetFileHash(dllPath);
                if (!actualHash.Equals(ExpectedGameAssemblyHash, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatus("HATA: Tanımsız oyun versiyonu!");
                    GamePathText.Text = "Tanımsız versiyon";
                    MessageBox.Show(
                        "Tanımsız oyun versiyonu!\n\n" +
                        $"Beklenen hash: {ExpectedGameAssemblyHash}\n" +
                        $"Gerçek hash: {actualHash}\n\n" +
                        "Bu yama sadece desteklenen oyun versiyonu içindir.\n" +
                        "Lütfen oyununuzu son Steam sürümüne güncelleyin.",
                        "Tanımsız Versiyon",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Hash kontrolü sırasında hata: {ex.Message}");
                MessageBox.Show(
                    $"GameAssembly.dll dosyası okunamadı!\n\n" +
                    $"Hata: {ex.Message}\n\n" +
                    "Oyunun kapalı olduğundan emin olun.",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            return true;
        }
    }
}
