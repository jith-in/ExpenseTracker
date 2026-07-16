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

        public Task<IEnumerable<SmsMessageData>> GetRecentSmsBodiesAsync()
        {
#if ANDROID
            return GetRecentSmsBodiesAndroidAsync();
#else
            return Task.FromResult<IEnumerable<SmsMessageData>>(new List<SmsMessageData>());
#endif
        }

#if ANDROID
        private static async Task<bool> CheckSmsPermissionAndroidAsync()
        {
            Debug.WriteLine("SmsReaderService: Checking READ_SMS permission.");

            // Resolved ambiguity using explicit fully qualified MAUI namespace bindings
            var status = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<Microsoft.Maui.ApplicationModel.Permissions.Sms>().ConfigureAwait(false);

            var granted = status == PermissionStatus.Granted;
            Debug.WriteLine($"SmsReaderService: READ_SMS permission granted={granted}.");
            return granted;
        }

        private static async Task<bool> RequestSmsPermissionAndroidAsync()
        {
            Debug.WriteLine("SmsReaderService: Requesting READ_SMS permission.");

            // Resolved ambiguity using explicit fully qualified MAUI namespace bindings
            var status = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.Sms>().ConfigureAwait(false);

            var granted = status == PermissionStatus.Granted;
            Debug.WriteLine($"SmsReaderService: READ_SMS permission request granted={granted}.");
            return granted;
        }

        private static DateTime GetAdjustedTargetDate(int year, int month)
        {
            DateTime targetDate = new DateTime(year, month, 25);

            while (targetDate.DayOfWeek == DayOfWeek.Saturday || targetDate.DayOfWeek == DayOfWeek.Sunday)
            {
                targetDate = targetDate.AddDays(-1);
            }

            return targetDate;
        }

        private static async Task<IEnumerable<SmsMessageData>> GetRecentSmsBodiesAndroidAsync()
        {
            var result = new List<SmsMessageData>();
            if (!await CheckSmsPermissionAndroidAsync().ConfigureAwait(false))
            {
                return result;
            }

            DateTime today = DateTime.Today;
            DateTime currentMonthTarget = GetAdjustedTargetDate(today.Year, today.Month);

            DateTime startDate;
            DateTime endDate;

            if (today >= currentMonthTarget)
            {
                startDate = currentMonthTarget;
                DateTime nextMonth = today.AddMonths(1);
                endDate = GetAdjustedTargetDate(nextMonth.Year, nextMonth.Month);
            }
            else
            {
                DateTime previousMonth = today.AddMonths(-1);
                startDate = GetAdjustedTargetDate(previousMonth.Year, previousMonth.Month);
                endDate = currentMonthTarget;
            }

            long startMillis = new DateTimeOffset(startDate).ToUnixTimeMilliseconds();
            long endMillis = new DateTimeOffset(endDate).ToUnixTimeMilliseconds();

            var uri = Telephony.Sms.Inbox.ContentUri;
            var contentResolver = Android.App.Application.Context?.ContentResolver;

            if (uri == null)
            {
                Debug.WriteLine("SmsReaderService: SMS Content URI is null.");
                return result;
            }

            var projection = new[] {
                Telephony.Sms.InterfaceConsts.Body,
                Telephony.Sms.InterfaceConsts.Date
            };

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

                while (cursor.MoveToNext())
                {
                    var body = cursor.GetString(bodyIndex);
                    long timestamp = cursor.GetLong(dateIndex);

                    DateTime receivedDate = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;

                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        result.Add(new SmsMessageData { Body = body, ReceivedDate = receivedDate });
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