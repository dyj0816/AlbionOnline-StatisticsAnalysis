﻿using log4net;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Enumerations;
using StatisticsAnalysisTool.Models;
using StatisticsAnalysisTool.Models.NetworkModel;
using StatisticsAnalysisTool.Properties;
using StatisticsAnalysisTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace StatisticsAnalysisTool.Network.Manager
{
    public class MailController
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);
        
        private readonly MainWindowViewModel _mainWindowViewModel;
        
        public List<MailInfoObject> CurrentMailInfos = new();

        public MailController(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;

            if (_mainWindowViewModel?.Mails != null)
            {
                _mainWindowViewModel.Mails.CollectionChanged += OnCollectionChanged;
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _mainWindowViewModel?.MailStatsObject.SetMailStats(_mainWindowViewModel.Mails);
        }

        public void SetMailInfos(List<MailInfoObject> currentMailInfos)
        {
            CurrentMailInfos.Clear();
            CurrentMailInfos.AddRange(currentMailInfos);
        }

        public void AddMail(long mailId, string content)
        {
            if (_mainWindowViewModel.Mails.ToArray().Any(x => x.MailId == mailId))
            {
                return;
            }

            var mailInfo = CurrentMailInfos.FirstOrDefault(x => x.MailId == mailId);

            if (mailInfo == null)
            {
                return;
            }

            var mailContent = ContentToObject(mailInfo.MailType, content);

            var mail = new Mail()
            {
                Tick = mailInfo.Tick,
                Guid = mailInfo.Guid ?? default,
                MailId = mailId,
                ClusterIndex = mailInfo.Subject,
                MailTypeText = mailInfo.MailTypeText,
                MailContent = mailContent
            };
            
            AddMailToListAndSort(mail);
        }

        public async void AddMailToListAndSort(Mail mail)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _mainWindowViewModel.Mails.Add(mail);
                _mainWindowViewModel.Mails.SortDescending(x => x.Tick);
            });
        }
        
        public void RemoveMailsByIdsAsync(IEnumerable<long> mailIds)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var mail in _mainWindowViewModel?.Mails?.ToList().Where(x => mailIds.Contains(x.MailId)) ?? new List<Mail>())
                {
                    _mainWindowViewModel?.Mails?.Remove(mail);
                }
            });
        }
        
        private static MailContent ContentToObject(MailType type, string content)
        {
            switch (type)
            {
                case MailType.MarketplaceBuyOrderFinished:
                case MailType.MarketplaceSellOrderFinished:
                    var contentObject = content.Split("|");

                    if (contentObject.Length < 3)
                    {
                        return new MailContent();
                    }

                    _ = int.TryParse(contentObject[0], out var quantity);
                    var uniqueItemName = contentObject[1];
                    _ = long.TryParse(contentObject[2], out var totalPriceLong);
                    _ = long.TryParse(contentObject[3], out var unitPriceLong);

                    return new MailContent()
                    {
                        Quantity = quantity,
                        InternalTotalPrice = totalPriceLong,
                        InternalUnitPrice = unitPriceLong,
                        UniqueItemName = uniqueItemName
                    };

                default:
                    return new MailContent();
            }
        }

        private void SetMails(List<Mail> mails)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var item in mails)
                {
                    _mainWindowViewModel?.Mails.Add(item);
                }

                _mainWindowViewModel?.Mails.SortDescending(x => x.Tick);
                _mainWindowViewModel?.MailStatsObject.SetMailStats(mails);
            });
        }

        /// <summary>
        /// Converted a string to MailType.
        /// </summary>
        /// <param name="typeString"></param>
        /// <returns>Returns a enum as MailType.</returns>
        public static MailType ConvertToMailType(string typeString)
        {
            return typeString switch
            {
                "MARKETPLACE_BUYORDER_FINISHED_SUMMARY" => MailType.MarketplaceBuyOrderFinished,
                "MARKETPLACE_SELLORDER_FINISHED_SUMMARY" => MailType.MarketplaceSellOrderFinished,
                _ => MailType.Unknown
            };
        }

        #region Load / Save local file data

        public void LoadFromFile()
        {
            var localFilePath = $"{AppDomain.CurrentDomain.BaseDirectory}{Settings.Default.MailsFileName}";

            if (File.Exists(localFilePath))
            {
                try
                {
                    var localFileString = File.ReadAllText(localFilePath, Encoding.UTF8);
                    var stats = JsonSerializer.Deserialize<List<Mail>>(localFileString) ?? new List<Mail>();
                    SetMails(stats);
                    return;
                }
                catch (Exception e)
                {
                    ConsoleManager.WriteLineForError(MethodBase.GetCurrentMethod()?.DeclaringType, e);
                    Log.Error(MethodBase.GetCurrentMethod()?.DeclaringType, e);
                    SetMails(new List<Mail>());
                    return;
                }
            }

            SetMails(new List<Mail>());
        }

        public void SaveInFile()
        {
            var localFilePath = $"{AppDomain.CurrentDomain.BaseDirectory}{Settings.Default.MailsFileName}";

            try
            {
                var fileString = JsonSerializer.Serialize(_mainWindowViewModel.Mails.ToList());
                File.WriteAllText(localFilePath, fileString, Encoding.UTF8);
            }
            catch (Exception e)
            {
                ConsoleManager.WriteLineForError(MethodBase.GetCurrentMethod()?.DeclaringType, e);
                Log.Error(MethodBase.GetCurrentMethod()?.DeclaringType, e);
            }
        }

        #endregion
    }
}