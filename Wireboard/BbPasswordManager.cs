using MahApps.Metro.Controls.Dialogs;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Wireboard
{
    public class BbPasswordManager
    {
        private class PasswordEntry
        {

            public byte[] Key;
            public int ServerGUID;

            public PasswordEntry(byte[] key, int serverGUID)
            {
                Key = key;
                ServerGUID = serverGUID;
            }
        }

        protected static String TAG = typeof(BbPasswordManager).Name;
        private const int ITERATIONS = 50000;
        private const String STOREDPASSWORDS_FILENAME = "keys.db";
        private const ushort STOREDPASSWORDS_FILEVERSION = 1;

        private static BbPasswordManager s_instance;

        private readonly IDialogCoordinator m_dialogCoordinator;
        private readonly List<PasswordEntry> m_liStoredPasswords = new List<PasswordEntry>();

        public BbPasswordManager(IDialogCoordinator dialogCoordinator)
        {
            s_instance = this;
            m_dialogCoordinator = dialogCoordinator;
        }

        public static async Task<byte[]> GetKeyForServer(int nServerGUID, String strServerName, byte[] abyPasswordSalt, bool bFailedBefore, CancellationToken cancelToken)
        {
            if (s_instance == null)
                return null;
            if (!bFailedBefore)
            {
                PasswordEntry entry = s_instance.m_liStoredPasswords.Find(x => x.ServerGUID == nServerGUID);
                if (entry != null)
                    return entry.Key;
            }
            return await s_instance.GetPasswordFromUser(nServerGUID, strServerName, abyPasswordSalt, bFailedBefore, cancelToken);

        }

        private async Task<byte[]> GetPasswordFromUser(int nServerGUID, String strServerName, byte[] abyPasswordSalt, bool bFailedBefore, CancellationToken cancelToken)
        {
            LoginDialogData result = null;
            try
            {
                String strText = "Enter the " + (string)Application.Current.FindResource("AppName") + " password for " + strServerName;
                if (bFailedBefore)
                    strText = "Authentication failed. " + strText;

                result = await m_dialogCoordinator.ShowLoginAsync(this, "Authentication", strText
                    , new LoginDialogSettings()
                    {
                        ColorScheme = MetroDialogColorScheme.Theme,
                        ShouldHideUsername = true,
                        EnablePasswordPreview = true,
                        RememberCheckBoxVisibility = Visibility.Visible,
                        NegativeButtonText = "Cancel",
                        NegativeButtonVisibility = Visibility.Visible,
                        AffirmativeButtonText = "OK",
                        CancellationToken = cancelToken
                    });
            }
            catch (Exception e)
            {
                Log.w(TAG, "LoginDialog Exception: " + e.Message);
                result = null;
            }
            if (result != null)
            {
                byte[] abyPassword = result.SecurePassword.ToByteArray();
                try
                {
                    byte[] key = await Task.Run(() =>
                    {
                        Pkcs5S1ParametersGenerator gen = new Pkcs5S1ParametersGenerator(new Sha256Digest());
                        gen.Init(abyPassword, abyPasswordSalt, ITERATIONS);
#pragma warning disable CS0618 // Type or member is obsolete
                        return ((KeyParameter)gen.GenerateDerivedParameters(256)).GetKey();
#pragma warning restore CS0618 // Type or member is obsolete
                    }, cancelToken);

                    if (result.ShouldRemember)
                        AddStoredPassword(nServerGUID, key);

                    return key;
                }
                catch (Exception e)
                {
                    if (!(e is OperationCanceledException))
                        Log.e(TAG, "Error while generating key from password: " + e.Message);
                }
                finally
                {
                    Arrays.Fill(abyPassword, 0);
                }
            }
            return null;
        }

        private void AddStoredPassword(int nServerGUID, byte[] key)
        {
            m_liStoredPasswords.RemoveAll(x => x.ServerGUID == nServerGUID);
            m_liStoredPasswords.Add(new PasswordEntry(key, nServerGUID));
            Task ignored = SavePasswords(); // we really don't want to (a)wait for the result
        }

        public async Task SavePasswords()
        {
            String strPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar
                + (string)Application.Current.FindResource("AppNameCode");

            // Usually very little data to read/write, however it can still take a long time (for example if a HD needs to spin up before access), so don't do it in the GUI thread
            await Task.Run(() =>
                {
                    try
                    {
                        Directory.CreateDirectory(strPath);
                        using (BinaryWriter writer = new BinaryWriter(new FileStream(strPath + Path.DirectorySeparatorChar + STOREDPASSWORDS_FILENAME, FileMode.Create, FileAccess.Write, FileShare.Read)
                            , Encoding.Default, false))
                        {
                            writer.Write(STOREDPASSWORDS_FILEVERSION);
                            writer.Write(m_liStoredPasswords.Count);
                            foreach (PasswordEntry entry in m_liStoredPasswords)
                            {
                                writer.Write(entry.ServerGUID);
                                writer.Write((UInt16)entry.Key.Length);
                                writer.Write(entry.Key);
                            }
                        }
                        Log.d(TAG, "Saved " + m_liStoredPasswords.Count + " stored passwords");
                    }
                    catch (Exception e)
                    {
                        Log.e(TAG, "Error while saving stored passwords: " + e.Message);
                    }
                });
            }

        public async Task LoadPasswords()
        {
            String strFullPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar
                + (string)Application.Current.FindResource("AppNameCode") + Path.DirectorySeparatorChar + STOREDPASSWORDS_FILENAME;
            if (!File.Exists(strFullPath))
            {
                Log.d(TAG, "Stored passwords file not found, 0 keys loaded");
                return;
            }
            // Usually very little data to read/write, however it can still take a long time (for example if a HD needs to spin up before access), so don't do it in the GUI thread
            await Task.Run(() =>
            {
                try
                {
                    using (BinaryReader reader = new BinaryReader(new FileStream(strFullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                        , Encoding.Default, false))
                    {
                        if (reader.ReadUInt16() != STOREDPASSWORDS_FILEVERSION)
                            throw new IOException("Fileversion unknown");
                        
                        for (int nCount = reader.ReadInt32(); nCount > 0; nCount--)
                        {
                            int nServerGUID = reader.ReadInt32();
                            UInt16 nKeyLen = reader.ReadUInt16();
                            byte[] key = new byte[nKeyLen];
                            reader.Read(key, 0, nKeyLen);
                            m_liStoredPasswords.Add(new PasswordEntry(key, nServerGUID));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.e(TAG, "Error while saving stored passwords: " + e.Message);
                }
            });
            Log.d(TAG, "Loaded " + m_liStoredPasswords.Count + " stored keys");
        }

        public static async Task ClearPasswords()
        {
            if (s_instance == null)
                return;
            s_instance.m_liStoredPasswords.Clear();
            await s_instance.SavePasswords();
            Log.i(TAG, "Deleted all stored passwords", true);
        }
    }
}
