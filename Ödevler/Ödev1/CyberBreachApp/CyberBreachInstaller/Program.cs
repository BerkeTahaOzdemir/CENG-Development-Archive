using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace CyberBreachInstaller
{
    /// <summary>
    /// CyberBreach Installer — One-time installation guard via Windows Registry.
    /// Uses HKEY_CURRENT_USER so no admin elevation is required.
    /// </summary>
    internal class Program
    {
        // ── Registry Constants ───────────────────────────────────────
        private const string RegistrySubKey = @"SOFTWARE\CyberBreach_License";
        private const string InstallDateValue = "InstallDate";
        private const string InstalledByValue = "InstalledBy";
        private const string VersionValue     = "Version";

        // ── Installer Config ─────────────────────────────────────────
        private const string AppVersion = "1.0.0";
        private const string AppExeName = "CyberBreachApp.exe";

        // ══════════════════════════════════════════════════════════════
        //  Entry Point
        // ══════════════════════════════════════════════════════════════
        static int Main(string[] args)
        {
            PrintBanner();

            try
            {
                // ── Step 1: Check if already installed ────────────────
                if (IsAlreadyInstalled())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
                    Console.WriteLine("  ║  HATA: Bu program daha önce bu bilgisayara kurulmuş.   ║");
                    Console.WriteLine("  ║  Tekil kurulum kuralı gereği kurulum engellendi.        ║");
                    Console.WriteLine("  ╚══════════════════════════════════════════════════════════╝");
                    Console.ResetColor();

                    // Show existing install info
                    DisplayExistingInstallInfo();

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  Çıkmak için Enter tuşuna basınız...");
                    Console.ResetColor();
                    WaitForInput();
                    return 1; // Exit with error code
                }

                // ── Step 2: Perform installation ─────────────────────
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  [1/3] Lisans kaydı oluşturuluyor...");
                Console.ResetColor();

                WriteRegistryLicense();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  [2/3] CyberBreachApp dosyaları kopyalanıyor...");
                Console.ResetColor();

                SimulateFileCopy();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  [3/3] Kurulum doğrulanıyor...");
                Console.ResetColor();

                if (!VerifyInstallation())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n  HATA: Kurulum doğrulaması başarısız oldu!");
                    Console.ResetColor();
                    WaitForInput();
                    return 2;
                }

                // ── Step 3: Success ──────────────────────────────────
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
                Console.WriteLine("  ║  Kurulum başarılı! CyberBreachApp dosyaları kopyalandı. ║");
                Console.WriteLine("  ╚══════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                // ── Step 4: Optionally launch the app ────────────────
                Console.WriteLine();
                Console.Write("  CyberBreachApp şimdi başlatılsın mı? (E/H): ");
                string? input = null;
                try { input = Console.ReadLine()?.Trim(); } catch { }

                if (input is "E" or "e")
                {
                    LaunchApp();
                }

                return 0; // Success
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  HATA: Yetki hatası — {ex.Message}");
                Console.WriteLine("  Lütfen uygulamayı yönetici olarak çalıştırmayı deneyin.");
                Console.ResetColor();
                WaitForInput();
                return 3;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  HATA: Beklenmeyen hata — {ex.Message}");
                Console.ResetColor();
                WaitForInput();
                return 99;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  Registry Operations
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks HKCU\SOFTWARE\CyberBreach_License for an existing installation.
        /// </summary>
        private static bool IsAlreadyInstalled()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistrySubKey);
            return key != null;
        }

        /// <summary>
        /// Creates the registry key and writes installation metadata.
        /// The entry persists in HKCU even after the installer closes.
        /// </summary>
        private static void WriteRegistryLicense()
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistrySubKey, writable: true);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string username = Environment.UserName;

            key.SetValue(InstallDateValue, timestamp, RegistryValueKind.String);
            key.SetValue(InstalledByValue, username, RegistryValueKind.String);
            key.SetValue(VersionValue, AppVersion, RegistryValueKind.String);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"        → Kayıt defteri: HKCU\\{RegistrySubKey}");
            Console.WriteLine($"        → InstallDate : {timestamp}");
            Console.WriteLine($"        → InstalledBy : {username}");
            Console.WriteLine($"        → Version     : {AppVersion}");
            Console.ResetColor();
        }

        /// <summary>
        /// Re-reads the registry to confirm the key was persisted.
        /// </summary>
        private static bool VerifyInstallation()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistrySubKey);
            if (key == null) return false;

            string? installDate = key.GetValue(InstallDateValue) as string;
            return !string.IsNullOrEmpty(installDate);
        }

        /// <summary>
        /// Displays information about an existing installation from the registry.
        /// </summary>
        private static void DisplayExistingInstallInfo()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistrySubKey);
            if (key == null) return;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("  ── Mevcut Kurulum Bilgisi ──────────────────────────────");

            string? installDate = key.GetValue(InstallDateValue) as string;
            string? installedBy = key.GetValue(InstalledByValue) as string;
            string? version     = key.GetValue(VersionValue) as string;

            if (installDate != null) Console.WriteLine($"     Kurulum Tarihi : {installDate}");
            if (installedBy != null) Console.WriteLine($"     Kuran Kullanıcı: {installedBy}");
            if (version != null)     Console.WriteLine($"     Sürüm          : {version}");

            Console.ResetColor();
        }

        // ══════════════════════════════════════════════════════════════
        //  Installation Simulation
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Simulates file copy operations with a progress display.
        /// </summary>
        private static void SimulateFileCopy()
        {
            string[] simulatedFiles =
            {
                "CyberBreachApp.exe",
                "CyberBreachApp.dll",
                "CyberBreachApp.runtimeconfig.json",
                "GameObject.dll",
                "Player.dll",
                "Firewall.dll",
                "DataBlast.dll",
                "assets/grid_background.dat",
                "assets/sfx_blast.wav",
                "config/settings.json"
            };

            Console.ForegroundColor = ConsoleColor.DarkGray;

            for (int i = 0; i < simulatedFiles.Length; i++)
            {
                int pct = (int)((i + 1) / (double)simulatedFiles.Length * 100);
                Console.Write($"\r        → [{pct,3}%] {simulatedFiles[i].PadRight(45)}");
                System.Threading.Thread.Sleep(200); // Simulated delay
            }

            Console.WriteLine();
            Console.ResetColor();
        }

        // ══════════════════════════════════════════════════════════════
        //  App Launcher
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Attempts to launch the CyberBreachApp executable from common locations.
        /// </summary>
        private static void LaunchApp()
        {
            // Look for the exe relative to the installer
            string installerDir = AppDomain.CurrentDomain.BaseDirectory;

            string[] searchPaths =
            {
                Path.Combine(installerDir, "..", AppExeName),
                Path.Combine(installerDir, "..", "bin", "Debug", "net10.0-windows", AppExeName),
                Path.Combine(installerDir, AppExeName),
            };

            foreach (string path in searchPaths)
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n  CyberBreachApp başlatılıyor...");
                    Console.ResetColor();

                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fullPath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  Uygulama başlatılamadı: {ex.Message}");
                        Console.ResetColor();
                    }
                    return;
                }
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  UYARI: {AppExeName} bulunamadı.");
            Console.WriteLine("  Lütfen CyberBreachApp projesini derleyip manuel olarak çalıştırın.");
            Console.ResetColor();
        }

        // ══════════════════════════════════════════════════════════════
        //  UI Helpers
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Prints the installer banner.
        /// </summary>
        private static void PrintBanner()
        {
            try { Console.Clear(); } catch { /* non-interactive console */ }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║           ░█▀▀ ▀█▀ ░█▀▀█ ░█▀▀█ ░█▀▀█                  ║");
            Console.WriteLine("  ║           ░█    ░█  ░█▀▀▄ ░█▀▀▄ ░█▄▄▀                  ║");
            Console.WriteLine("  ║           ░█▄▄ ▄█▄ ░█▄▄█ ░█▄▄█ ░█ ░█                  ║");
            Console.WriteLine("  ║                                                         ║");
            Console.WriteLine("  ║           C Y B E R B R E A C H    v1.0                 ║");
            Console.WriteLine("  ║                  Kurulum Sihirbazı                      ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        /// <summary>
        /// Waits for user input, gracefully handling redirected consoles.
        /// </summary>
        private static void WaitForInput()
        {
            try
            {
                if (Console.IsInputRedirected)
                    return;
                Console.ReadLine();
            }
            catch { /* redirected / non-interactive console */ }
        }
    }
}
