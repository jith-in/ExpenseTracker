using ExpenseTracker.Interfaces;
using Microsoft.Maui.ApplicationModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ExpenseTracker.Models;
using System;
using System.Linq;

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

        public Task<IEnumerable<SmsMessageData>> GetRecentSmsBodiesAsync(int maxMessages = 100)
        {
#if ANDROID
            return GetRecentSmsBodiesAndroidAsync(maxMessages);
#else
            // Must return Task<IEnumerable<SmsMessageData>>
            return Task.FromResult<IEnumerable<SmsMessageData>>(new List<SmsMessageData>());
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

        private static async Task<IEnumerable<SmsMessageData>> GetRecentSmsBodiesAndroidAsync(int maxMessages)
        {
            var result = new List<SmsMessageData>();
            if (!await CheckSmsPermissionAndroidAsync().ConfigureAwait(false))
            {
                return result;
            }

            // 1. Calculate the 20th-to-20th cycle boundaries
            DateTime today = DateTime.Today;
            DateTime startDate = today.Day >= 20
                ? new DateTime(today.Year, today.Month, 20)
                : new DateTime(today.Year, today.Month, 20).AddMonths(-1);

            DateTime endDate = startDate.AddMonths(1);

            long startMillis = new DateTimeOffset(startDate).ToUnixTimeMilliseconds();
            long endMillis = new DateTimeOffset(endDate).ToUnixTimeMilliseconds();

            // 2. Prepare Query
            var uri = Telephony.Sms.Inbox.ContentUri;
            var contentResolver = Android.App.Application.Context?.ContentResolver;

            var projection = new[] {
                Telephony.Sms.InterfaceConsts.Body,
                Telephony.Sms.InterfaceConsts.Date
            };

            // Use SQL selection to filter at the database level
            string selection = $"{Telephony.Sms.InterfaceConsts.Date} >= ? AND {Telephony.Sms.InterfaceConsts.Date} < ?";
            string[] selectionArgs = { startMillis.ToString(), endMillis.ToString() };

            var sortOrder = Telephony.Sms.DefaultSortOrder;

            ICursor? cursor = null;
            try
            {
                cursor = contentResolver?.Query(uri, projection, selection, selectionArgs, sortOrder);
                if (cursor == null) return result;

                var bodyIndex = cursor.GetColumnIndexOrThrow(Telephony.Sms.InterfaceConsts.Body);
                var dateIndex = cursor.GetColumnIndexOrThrow(Telephony.Sms.InterfaceConsts.Date);

                var count = 0;
                while (cursor.MoveToNext() && count < maxMessages)
                {
                    var body = cursor.GetString(bodyIndex);
                    long timestamp = cursor.GetLong(dateIndex);

                    // Convert milliseconds to local time
                    DateTime receivedDate = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;

                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        result.Add(new SmsMessageData { Body = body, ReceivedDate = receivedDate });
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SmsReaderService: Error reading filtered SMS: {ex}");
            }
            finally
            {
                cursor?.Close();
                cursor?.Dispose();
            }

            return result;
        }
#endif
    }
}