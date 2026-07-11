using ExpenseTracker.Interfaces;
using Microsoft.Maui.ApplicationModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

#if ANDROID
using Android.App;
using Android.Content;
using Android.Database;
using Android.Provider;
#endif

namespace ExpenseTracker.Services
{
    public class SmsReaderService : ISmsReaderService
    {
        public Task<bool> CheckSmsPermissionAsync()
        {
#if ANDROID
            return CheckSmsPermissionAndroidAsync();
#else
            Debug.WriteLine("SmsReaderService: SMS permission is not supported on this platform.");
            return Task.FromResult(false);
#endif
        }

        public Task<bool> RequestSmsPermissionAsync()
        {
#if ANDROID
            return RequestSmsPermissionAndroidAsync();
#else
            Debug.WriteLine("SmsReaderService: SMS permission request is not supported on this platform.");
            return Task.FromResult(false);
#endif
        }

        public Task<IEnumerable<string>> GetRecentSmsBodiesAsync(int maxMessages = 100)
        {
#if ANDROID
            return GetRecentSmsBodiesAndroidAsync(maxMessages);
#else
            return Task.FromResult<IEnumerable<string>>(new List<string>());
#endif
        }

#if ANDROID
        private static async Task<bool> CheckSmsPermissionAndroidAsync()
        {
            Debug.WriteLine("SmsReaderService: Checking READ_SMS permission.");
            var status = await Permissions.CheckStatusAsync<Permissions.Sms>().ConfigureAwait(false);
            var granted = status == PermissionStatus.Granted;
            Debug.WriteLine($"SmsReaderService: READ_SMS permission granted={granted}.");
            return granted;
        }

        private static async Task<bool> RequestSmsPermissionAndroidAsync()
        {
            Debug.WriteLine("SmsReaderService: Requesting READ_SMS permission.");
            var status = await Permissions.RequestAsync<Permissions.Sms>().ConfigureAwait(false);
            var granted = status == PermissionStatus.Granted;
            Debug.WriteLine($"SmsReaderService: READ_SMS permission request granted={granted}.");
            return granted;
        }

        private static async Task<IEnumerable<string>> GetRecentSmsBodiesAndroidAsync(int maxMessages)
        {
            var result = new List<string>();
            if (!await CheckSmsPermissionAndroidAsync().ConfigureAwait(false))
            {
                Debug.WriteLine("SmsReaderService: READ_SMS permission not granted, skipping SMS read.");
                return result;
            }

            var uri = Telephony.Sms.Inbox.ContentUri;
            if (uri == null)
            {
                Debug.WriteLine("SmsReaderService: Telephony.Sms.Inbox.ContentUri is null.");
                return result;
            }

            var contentResolver = Android.App.Application.Context?.ContentResolver;
            if (contentResolver == null)
            {
                Debug.WriteLine("SmsReaderService: Android application context or ContentResolver is null.");
                return result;
            }

            var projection = new[] { Telephony.Sms.InterfaceConsts.Body };
            var sortOrder = Telephony.Sms.DefaultSortOrder;

            ICursor? cursor = null;
            try
            {
                cursor = contentResolver.Query(uri, projection, null, null, sortOrder);
                if (cursor == null)
                {
                    Debug.WriteLine("SmsReaderService: SMS query returned null cursor.");
                    return result;
                }

                var bodyIndex = cursor.GetColumnIndex(Telephony.Sms.InterfaceConsts.Body);
                var count = 0;
                while (cursor.MoveToNext() && count < maxMessages)
                {
                    var body = cursor.GetString(bodyIndex);
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        result.Add(body);
                        count++;
                    }
                }

                Debug.WriteLine($"SmsReaderService: Read {result.Count} SMS messages.");
            }
            catch (Java.Lang.SecurityException ex)
            {
                Debug.WriteLine($"SmsReaderService: SecurityException while reading SMS: {ex}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SmsReaderService: Exception while reading SMS: {ex}");
            }
            finally
            {
                if (cursor != null)
                {
                    try
                    {
                        cursor.Close();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SmsReaderService: Failed to close SMS cursor: {ex}");
                    }
                    finally
                    {
                        cursor.Dispose();
                    }
                }
            }

            return result;
        }
#endif
    }
}
