﻿using log4net;
using Newtonsoft.Json;
using PcapDotNet.Base;
using StatisticsAnalysisTool.Enumerations;
using StatisticsAnalysisTool.GameData;
using StatisticsAnalysisTool.Models.NetworkModel;
using StatisticsAnalysisTool.Network.Notification;
using StatisticsAnalysisTool.Properties;
using StatisticsAnalysisTool.ViewModels;
using StatisticsAnalysisTool.Views;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ValueType = StatisticsAnalysisTool.Enumerations.ValueType;

namespace StatisticsAnalysisTool.Network.Controller
{
    public class DungeonController
    {
        private readonly TrackingController _trackingController;
        private readonly MainWindow _mainWindow;
        private readonly MainWindowViewModel _mainWindowViewModel;

        private Guid? _lastGuid;
        private Guid? _currentGuid;
        public LocalUserData LocalUserData { get; set; }

        private const int _maxDungeons = 9999;

        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public DungeonController(TrackingController trackingController, MainWindow mainWindow, MainWindowViewModel mainWindowViewModel)
        {
            _trackingController = trackingController;
            _mainWindow = mainWindow;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public void AddDungeon(MapType mapType, Guid? mapGuid, string mainMapIndex)
        {
            LeaveDungeonCheck(mapType);
            IsDungeonDoneCheck(mapType);
            SetBestDungeonTime();
            SetBestDungeonValuesWithDispatcher();

            if (mapType != MapType.RandomDungeon || mapGuid == null)
            {
                if (_lastGuid != null)
                {
                    SetCurrentDungeonActive((Guid)_lastGuid, true);
                }

                _currentGuid = null;
                _lastGuid = null;
                return;
            }

            try
            {
                _currentGuid = (Guid)mapGuid;
                var currentGuid = (Guid)_currentGuid;

                AddDungeonRunIfNextMap(currentGuid);
                SetNewStartTimeWhenOneMoreTimeEnter(currentGuid);

                if (_lastGuid != null && !_mainWindowViewModel.TrackingDungeons.Any(x => x.MapsGuid.Contains(currentGuid)))
                {
                    AddMapToExistDungeon(currentGuid, (Guid)_lastGuid);

                    _lastGuid = currentGuid;
                    _mainWindowViewModel.DungeonStatsDay.EnteredDungeon = GetDungeonsCount(DateTime.UtcNow.AddDays(-1));
                    _mainWindowViewModel.DungeonStatsTotal.EnteredDungeon = GetDungeonsCount(DateTime.UtcNow.AddYears(-10));

                    RemoveDungeonsAfterCertainNumber(_maxDungeons);
                    SetCurrentDungeonActive(currentGuid);
                    return;
                }

                if (_lastGuid == null && !_mainWindowViewModel.TrackingDungeons.Any(x => x.MapsGuid.Contains((Guid)mapGuid)))
                {
                    if (_mainWindow.Dispatcher.CheckAccess())
                    {
                        _mainWindowViewModel.TrackingDungeons.Insert(
                            0,
                            new DungeonNotificationFragment(currentGuid, _mainWindowViewModel.TrackingDungeons.Count + 1, mainMapIndex, DateTime.UtcNow));
                    }
                    else
                    {
                        _mainWindow.Dispatcher.Invoke(delegate
                        {
                            _mainWindowViewModel.TrackingDungeons.Insert(
                                0,
                                new DungeonNotificationFragment(currentGuid, _mainWindowViewModel.TrackingDungeons.Count + 1, mainMapIndex, DateTime.UtcNow));
                        });
                    }

                    _lastGuid = mapGuid;
                    _mainWindowViewModel.DungeonStatsDay.EnteredDungeon = GetDungeonsCount(DateTime.UtcNow.AddDays(-1));
                    _mainWindowViewModel.DungeonStatsTotal.EnteredDungeon = GetDungeonsCount(DateTime.UtcNow.AddYears(-10));

                    RemoveDungeonsAfterCertainNumber(_maxDungeons);
                    SetCurrentDungeonActive(currentGuid);
                    return;
                }

                SetCurrentDungeonActive(currentGuid);
                _lastGuid = currentGuid;
            }
            catch
            {
                _currentGuid = null;
            }
        }

        public void SetDungeonChestOpen(int id)
        {
            if (_currentGuid != null)
            {
                try
                {
                    var dun = GetCurrentDungeon((Guid)_currentGuid);
                    var chest = dun?.DungeonChests?.FirstOrDefault(x => x.Id == id);

                    if (chest == null)
                    {
                        return;
                    }

                    chest.IsChestOpen = true;
                    chest.Opened = DateTime.UtcNow;

                }
                catch (Exception e)
                {
                    Log.Error(nameof(SetDungeonChestOpen), e);
                }
            }

            SetDungeonStatsDay();
            SetDungeonStatsTotal();
        }

        public void SetDungeonChestInformation(int id, string uniqueName)
        {
            if (_currentGuid != null && uniqueName != null)
            {
                try
                {
                    var dun = GetCurrentDungeon((Guid)_currentGuid);

                    if (dun == null || _currentGuid == null || dun.DungeonChests?.Any(x => x.Id == id) == true)
                    {
                        return;
                    }

                    var dunChest = new DungeonChestFragment
                    {
                        UniqueName = uniqueName,
                        IsBossChest = LootChestData.IsBossChest(uniqueName),
                        IsChestOpen = false,
                        Id = id
                    };

                    if (_mainWindow.Dispatcher.CheckAccess())
                    {
                        dun.DungeonChests?.Add(dunChest);
                    }
                    else
                    {
                        _mainWindow.Dispatcher.Invoke(delegate
                        {
                            dun.DungeonChests?.Add(dunChest);
                        });
                    }

                    dun.Faction = LootChestData.GetFaction(uniqueName);
                    dun.Mode = LootChestData.GetDungeonMode(uniqueName);
                }
                catch (Exception e)
                {
                    Log.Error(nameof(SetDungeonChestInformation), e);
                }
            }
        }

        private DungeonNotificationFragment GetCurrentDungeon(Guid guid)
        {
            return _mainWindowViewModel.TrackingDungeons.FirstOrDefault(x => x.MapsGuid.Contains(guid));
        }

        private int GetChests(DateTime? chestIsNewerAsDateTime, ChestRarity rarity)
        {
            var dungeons = _mainWindowViewModel.TrackingDungeons.Where(x => x.StartDungeon > chestIsNewerAsDateTime || chestIsNewerAsDateTime == null);
            return dungeons.Select(dun => dun.DungeonChests.Where(x => x.Rarity == rarity)).Select(filteredChests => filteredChests.Count()).Sum();
        }

        public int GetDungeonsCount(DateTime dungeonIsNewerAsDateTime) => _mainWindowViewModel.TrackingDungeons.Count(x => x.StartDungeon > dungeonIsNewerAsDateTime);

        public void SetDungeonStatsDay()
        {
            _mainWindowViewModel.DungeonStatsDay.OpenedStandardChests = GetChests(DateTime.UtcNow.AddDays(-1), ChestRarity.Standard);
            _mainWindowViewModel.DungeonStatsDay.OpenedUncommonChests = GetChests(DateTime.UtcNow.AddDays(-1), ChestRarity.Uncommon);
            _mainWindowViewModel.DungeonStatsDay.OpenedRareChests = GetChests(DateTime.UtcNow.AddDays(-1), ChestRarity.Rare);
            _mainWindowViewModel.DungeonStatsDay.OpenedLegendaryChests = GetChests(DateTime.UtcNow.AddDays(-1), ChestRarity.Legendary);
        }

        public void SetDungeonStatsTotal()
        {
            _mainWindowViewModel.DungeonStatsTotal.OpenedStandardChests = GetChests(null, ChestRarity.Standard);
            _mainWindowViewModel.DungeonStatsTotal.OpenedUncommonChests = GetChests(null, ChestRarity.Uncommon);
            _mainWindowViewModel.DungeonStatsTotal.OpenedRareChests = GetChests(null, ChestRarity.Rare);
            _mainWindowViewModel.DungeonStatsTotal.OpenedLegendaryChests = GetChests(null, ChestRarity.Legendary);
        }

        public void SetDiedIfInDungeon(DiedObject dieObject)
        {
            if (_currentGuid != null && LocalUserData.Username != null && dieObject.DiedName == LocalUserData.Username)
            {
                try
                {
                    var item = _mainWindowViewModel.TrackingDungeons.First(x => x.MapsGuid.Contains((Guid)_currentGuid) && x.StartDungeon > DateTime.UtcNow.AddDays(-1));
                    item.DiedName = dieObject.DiedName;
                    item.KilledBy = dieObject.KilledBy;
                    item.DiedInDungeon = true;
                }
                catch (Exception e)
                {
                    Log.Error(nameof(SetDiedIfInDungeon), e);
                }
            }
        }

        private void RemoveDungeonsAfterCertainNumber(int amount)
        {
            if (_trackingController.IsMainWindowNull() || _mainWindowViewModel.TrackingDungeons == null)
            {
                return;
            }

            try
            {
                while (true)
                {
                    if (_mainWindowViewModel.TrackingNotifications?.Count <= amount)
                    {
                        break;
                    }

                    var dateTime = GetLowestDate(_mainWindowViewModel.TrackingDungeons);
                    if (dateTime != null)
                    {
                        var removableItem = _mainWindowViewModel.TrackingDungeons?.FirstOrDefault(x => x.StartDungeon == dateTime);
                        if (removableItem != null)
                        {
                            if (_mainWindow.Dispatcher.CheckAccess())
                            {
                                _mainWindowViewModel.TrackingDungeons.Remove(removableItem);
                            }
                            else
                            {
                                _mainWindow.Dispatcher.Invoke(delegate
                                {
                                    _mainWindowViewModel.TrackingDungeons.Remove(removableItem);
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(nameof(RemoveDungeonsAfterCertainNumber), e);
            }
        }

        private void SetCurrentDungeonActive(Guid guid, bool allToFalse = false)
        {
            if (_mainWindowViewModel.TrackingDungeons.Count <= 0)
            {
                return;
            }

            if (_mainWindow.Dispatcher.CheckAccess())
            {
                _mainWindowViewModel.TrackingDungeons.Where(x => x.Status != DungeonStatus.Done).ToList().ForEach(x => x.Status = DungeonStatus.Done);

                var dun = _mainWindowViewModel.TrackingDungeons?.First(x => x.MapsGuid.Contains(guid));
                if (!allToFalse)
                {
                    dun.Status = DungeonStatus.Active;
                }
            }
            else
            {
                _mainWindow.Dispatcher.Invoke(delegate
                {
                    _mainWindowViewModel.TrackingDungeons.Where(x => x.Status != DungeonStatus.Done).ToList().ForEach(x => x.Status = DungeonStatus.Done);

                    var dun = _mainWindowViewModel.TrackingDungeons?.First(x => x.MapsGuid.Contains(guid));
                    if (!allToFalse)
                    {
                        dun.Status = DungeonStatus.Active;
                    }
                });
            }
        }

        private void SetBestDungeonTime()
        {
            if (_mainWindow.Dispatcher.CheckAccess())
            {
                if (_mainWindowViewModel?.TrackingDungeons?.Any(x => x?.Status == DungeonStatus.Done && x.DungeonChests.Any(y => y?.IsBossChest == true)) == true)
                {
                    _mainWindowViewModel.TrackingDungeons.Where(x => x?.IsBestTime == true).ToList().ForEach(x => x.IsBestTime = false);
                    var min = _mainWindowViewModel?.TrackingDungeons?.Where(x => x?.DungeonChests.Any(y => y.IsBossChest) == true).Select(x => x.TotalTime).Min();
                    var bestTimeDungeon = _mainWindowViewModel?.TrackingDungeons?.SingleOrDefault(x => x.TotalTime == min);
                    if (bestTimeDungeon != null)
                    {
                        bestTimeDungeon.IsBestTime = true;
                    }
                }
            }
            else
            {
                _mainWindow.Dispatcher.Invoke(delegate
                {
                    _mainWindowViewModel.TrackingDungeons.Where(x => x?.IsBestTime == true).ToList().ForEach(x => x.IsBestTime = false);

                    if (_mainWindowViewModel?.TrackingDungeons?.Any(x => x?.Status == DungeonStatus.Done && x.DungeonChests.Any(y => y?.IsBossChest == true)) == true)
                    {
                        _mainWindowViewModel.TrackingDungeons.Where(x => x?.IsBestTime == true).ToList().ForEach(x => x.IsBestTime = false);
                        var min = _mainWindowViewModel?.TrackingDungeons?.Where(x => x?.DungeonChests.Any(y => y.IsBossChest) == true).Select(x => x.TotalTime).Min();
                        var bestTimeDungeon = _mainWindowViewModel?.TrackingDungeons?.SingleOrDefault(x => x.TotalTime == min);
                        if (bestTimeDungeon != null)
                        {
                            bestTimeDungeon.IsBestTime = true;
                        }
                    }
                });
            }
        }

        private void SetBestDungeonValuesWithDispatcher()
        {
            if (_mainWindow.Dispatcher.CheckAccess())
            {
                CalculateBestDungeonValues();
            }
            else
            {
                _mainWindow.Dispatcher.Invoke(CalculateBestDungeonValues);
            }
        }

        private void CalculateBestDungeonValues()
        {
            try
            {
                _mainWindowViewModel.TrackingDungeons.Where(x =>
                    x?.IsBestFame == true ||
                    x?.IsBestReSpec == true ||
                    x?.IsBestSilver == true ||
                    x?.IsBestFamePerHour == true ||
                    x?.IsBestReSpecPerHour == true ||
                    x?.IsBestSilverPerHour == true
                ).ToList().ForEach(x =>
                {
                    x.IsBestFame = false;
                    x.IsBestReSpec = false;
                    x.IsBestSilver = false;
                    x.IsBestFamePerHour = false;
                    x.IsBestReSpecPerHour = false;
                    x.IsBestSilverPerHour = false;
                });

                if (_mainWindowViewModel?.TrackingDungeons?.Any(x => x?.Status == DungeonStatus.Done && x.Fame > 0) == true)
                {
                    var highestFame = _mainWindowViewModel.TrackingDungeons.Where(x => x?.Status == DungeonStatus.Done && x.Fame > 0).Select(x => x.Fame).Max();
                    var bestDungeonFame = _mainWindowViewModel?.TrackingDungeons?.FirstOrDefault(x => x.Fame.CompareTo(highestFame) == 0);

                    if (bestDungeonFame != null)
                    {
                        bestDungeonFame.IsBestFame = true;
                    }
                }

                if (_mainWindowViewModel?.TrackingDungeons?.Any(x => x?.Status == DungeonStatus.Done && x.ReSpec > 0) == true)
                {
                    var highestReSpec = _mainWindowViewModel.TrackingDungeons.Where(x => x?.Status == DungeonStatus.Done && x.ReSpec > 0).Select(x => x.ReSpec).Max();
                    var bestDungeonReSpec = _mainWindowViewModel?.TrackingDungeons?.FirstOrDefault(x => x.ReSpec.CompareTo(highestReSpec) == 0);

                    if (bestDungeonReSpec != null)
                    {
                        bestDungeonReSpec.IsBestReSpec = true;
                    }
                }

                if (_mainWindowViewModel?.TrackingDungeons?.Any(x => x?.Status == DungeonStatus.Done && x.Silver > 0) == true)
                {
                    var highestSilver = _mainWindowViewModel.TrackingDungeons.Where(x => x?.Status == DungeonStatus.Done && x.Silver > 0).Select(x => x.Silver).Max();
                    var bestDungeonSilver = _mainWindowViewModel?.TrackingDungeons?.FirstOrDefault(x => x.Silver.CompareTo(highestSilver) == 0);

                    if (bestDungeonSilver != null)
                    {
                        bestDungeonSilver.IsBestSilver = true;
                    }
                }

                if (_mainWindowViewModel?.TrackingDungeons?.Any(x => x?.Status == DungeonStatus.Done && x.FamePerHour > 0) == true)
                {
                    var highestFamePerHour = _mainWindowViewModel.TrackingDungeons.Where(x => x?.Status == DungeonStatus.Done && x.FamePerHour > 0).Select(x => x.FamePerHour).Max();
                    var bestDungeonFamePerHour = _mainWindowViewModel?.TrackingDungeons?.FirstOrDefault(x => x.FamePerHour.CompareTo(highestFamePerHour) == 0);

                    if (bestDungeonFamePerHour != null)
                    {
                        bestDungeonFamePerHour.IsBestFamePerHour = true;
                    }
                }

                if (_mainWindowViewModel?.TrackingDungeons?.Any(x => x?.Status == DungeonStatus.Done && x.ReSpecPerHour > 0) == true)
                {
                    var highestReSpecPerHour = _mainWindowViewModel.TrackingDungeons.Where(x => x?.Status == DungeonStatus.Done && x.ReSpecPerHour > 0).Select(x => x.ReSpecPerHour).Max();
                    var bestDungeonReSpecPerHour = _mainWindowViewModel?.TrackingDungeons?.FirstOrDefault(x => x.ReSpecPerHour.CompareTo(highestReSpecPerHour) == 0);

                    if (bestDungeonReSpecPerHour != null)
                    {
                        bestDungeonReSpecPerHour.IsBestReSpecPerHour = true;
                    }
                }

                if (_mainWindowViewModel?.TrackingDungeons?.Any(x => x?.Status == DungeonStatus.Done && x.SilverPerHour > 0) == true)
                {
                    var highestSilverPerHour = _mainWindowViewModel.TrackingDungeons.Where(x => x?.Status == DungeonStatus.Done && x.SilverPerHour > 0).Select(x => x.SilverPerHour).Max();
                    var bestDungeonSilverPerHour = _mainWindowViewModel?.TrackingDungeons?.FirstOrDefault(x => x.SilverPerHour.CompareTo(highestSilverPerHour) == 0);

                    if (bestDungeonSilverPerHour != null)
                    {
                        bestDungeonSilverPerHour.IsBestSilverPerHour = true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(nameof(CalculateBestDungeonValues), e);
            }
        }

        private void AddDungeonRunIfNextMap(Guid currentGuid)
        {
            if (_lastGuid != null && _mainWindowViewModel.TrackingDungeons.Any(x => x.MapsGuid.Contains(currentGuid) && x.MapsGuid.Contains((Guid)_lastGuid)))
            {
                var dun = _mainWindowViewModel.TrackingDungeons?.First(x => x.MapsGuid.Contains(currentGuid));
                dun.AddDungeonRunTime(DateTime.UtcNow);
            }
        }

        private void SetNewStartTimeWhenOneMoreTimeEnter(Guid currentGuid)
        {
            if (_mainWindowViewModel.TrackingDungeons.Any(x => x.MapsGuid.Contains(currentGuid)))
            {
                if (_mainWindow.Dispatcher.CheckAccess())
                {
                    var dun = _mainWindowViewModel.TrackingDungeons?.First(x => x.MapsGuid.Contains(currentGuid));
                    dun.EnterDungeonMap = DateTime.UtcNow;
                }
                else
                {
                    _mainWindow.Dispatcher.Invoke(delegate
                    {
                        var dun = _mainWindowViewModel.TrackingDungeons?.First(x => x.MapsGuid.Contains(currentGuid));
                        dun.EnterDungeonMap = DateTime.UtcNow;
                    });
                }
            }
        }

        private void AddMapToExistDungeon(Guid currentGuid, Guid lastGuid)
        {
            if (_mainWindow.Dispatcher.CheckAccess())
            {
                var dun = _mainWindowViewModel.TrackingDungeons?.First(x => x.MapsGuid.Contains(lastGuid));
                dun?.MapsGuid.Add(currentGuid);
            }
            else
            {
                _mainWindow.Dispatcher.Invoke(delegate
                {
                    var dun = _mainWindowViewModel.TrackingDungeons?.First(x => x.MapsGuid.Contains(lastGuid));
                    dun?.MapsGuid.Add(currentGuid);
                });
            }
        }

        private void LeaveDungeonCheck(MapType mapType)
        {
            if (_lastGuid != null && _mainWindowViewModel.TrackingDungeons.Any(x => x.MapsGuid.Contains((Guid)_lastGuid)) && mapType != MapType.RandomDungeon)
            {
                var dun = _mainWindowViewModel.TrackingDungeons?.First(x => x.MapsGuid.Contains((Guid)_lastGuid));
                dun.AddDungeonRunTime(DateTime.UtcNow);
            }
        }

        private void IsDungeonDoneCheck(MapType mapType)
        {
            if (_lastGuid != null && _mainWindowViewModel.TrackingDungeons.Any(x => x.MapsGuid.Contains((Guid)_lastGuid)) && mapType != MapType.RandomDungeon)
            {
                var dun = _mainWindowViewModel?.TrackingDungeons?.FirstOrDefault(x =>
                    x.MapsGuid.Contains((Guid)_lastGuid) && x.DungeonChests.Any(y => y.IsBossChest));
                if (dun != null)
                {
                    dun.Status = DungeonStatus.Done;
                }
            }
        }

        public void AddValueToDungeon(double value, ValueType valueType)
        {
            if (_currentGuid == null)
            {
                return;
            }

            try
            {
                if (_mainWindow.Dispatcher.CheckAccess())
                {
                    var dun = _mainWindowViewModel.TrackingDungeons?.FirstOrDefault(x => x.MapsGuid.Contains((Guid)_currentGuid) && x.Status == DungeonStatus.Active);
                    dun?.Add(value, valueType);
                }
                else
                {
                    _mainWindow.Dispatcher.Invoke(delegate
                    {
                        var dun = _mainWindowViewModel.TrackingDungeons?.FirstOrDefault(x => x.MapsGuid.Contains((Guid)_currentGuid) && x.Status == DungeonStatus.Active);
                        dun?.Add(value, valueType);
                    });
                }
            }
            catch
            {
                // ignored
            }
        }

        public static DateTime? GetLowestDate(ObservableCollection<DungeonNotificationFragment> items)
        {
            if (items.IsNullOrEmpty())
            {
                return null;
            }

            try
            {
                return items.Select(x => x.StartDungeon).Min();
            }
            catch (ArgumentNullException e)
            {
                Log.Error(nameof(GetLowestDate), e);
                return null;
            }
        }

        public ObservableCollection<DungeonNotificationFragment> LoadDungeonFromFile()
        {
            var localFilePath = $"{AppDomain.CurrentDomain.BaseDirectory}{Settings.Default.DungeonRunsFileName}";

            if (File.Exists(localFilePath))
            {
                try
                {
                    var localItemString = File.ReadAllText(localFilePath, Encoding.UTF8);
                    return JsonConvert.DeserializeObject<ObservableCollection<DungeonNotificationFragment>>(localItemString);
                }
                catch (Exception e)
                {
                    Log.Error(nameof(LoadDungeonFromFile), e);
                    return new ObservableCollection<DungeonNotificationFragment>();
                }
            }
            return new ObservableCollection<DungeonNotificationFragment>();
        }

        public void SaveDungeonsInFile(ObservableCollection<DungeonNotificationFragment> dungeons)
        {
            var localFilePath = $"{AppDomain.CurrentDomain.BaseDirectory}{Settings.Default.DungeonRunsFileName}";

            try
            {
                var toSaveDungeons = dungeons.Where(x => x != null && x.Status == DungeonStatus.Done && x.TotalTime.Ticks > 0);
                var fileString = JsonConvert.SerializeObject(toSaveDungeons);
                File.WriteAllText(localFilePath, fileString, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Log.Error(nameof(SaveDungeonsInFile), e);
            }
        }
    }
}