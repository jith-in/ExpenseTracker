using ExpenseTracker.Interfaces;
using Microsoft.Maui.ApplicationModel;
using System.Collections.Generic;
using System.Threading.Tasks;

#if ANDROID
using Android.App;
using Android.Content;
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
            return Task.FromResult(false);
#endif
        }

        public Task<bool> RequestSmsPermissionAsync()
        {
#if ANDROID
            return RequestSmsPermissionAndroidAsync();
#else
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
            var status = await Permissions.CheckStatusAsync<Permissions.Sms>().ConfigureAwait(false);
            return status == PermissionStatus.Granted;
        }

        private static async Task<bool> RequestSmsPermissionAndroidAsync()
        {
            var status = await Permissions.RequestAsync<Permissions.Sms>().ConfigureAwait(false);
            return status == PermissionStatus.Granted;
        }

        private static Task<IEnumerable<string>> GetRecentSmsBodiesAndroidAsync(int maxMessages)
        {
            var result = new List<string>();
            var uri = Telephony.Sms.Inbox.ContentUri;
            if (uri == null)
            {
                return Task.FromResult<IEnumerable<string>>(result);
            }

            var contentResolver = Android.App.Application.Context?.ContentResolver;
            if (contentResolver == null)
            {
                return Task.FromResult<IEnumerable<string>>(result);
            }

            var projection = new[] { Telephony.Sms.InterfaceConsts.Body };
            var sortOrder = Telephony.Sms.DefaultSortOrder;

            using var cursor = contentResolver.Query(uri, projection, null, null, sortOrder);
            if (cursor == null)
            {
                return Task.FromResult<IEnumerable<string>>(result);
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

            return Task.FromResult<IEnumerable<string>>(result);
        }
#endif
    }
}
