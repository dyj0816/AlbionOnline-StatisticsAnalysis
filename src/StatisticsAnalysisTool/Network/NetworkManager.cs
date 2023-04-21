using log4net;
using PacketDotNet;
using SharpPcap;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Common.UserSettings;
using StatisticsAnalysisTool.Enumerations;
using StatisticsAnalysisTool.Network.Handler;
using StatisticsAnalysisTool.Network.Manager;
using StatisticsAnalysisTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace StatisticsAnalysisTool.Network;

public class NetworkManager
{
    private static IPhotonReceiver _receiver;
    private static readonly List<ICaptureDevice> CapturedDevices = new();
    private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);
    private static DateTime _lastGetCurrentServerByIpTime = DateTime.MinValue;
    private static int _serverEventCounter;
    private static AlbionServer _lastServerType;

    public static AlbionServer AlbionServer { get; private set; } = AlbionServer.Unknown;
    public static bool IsNetworkCaptureRunning => CapturedDevices.Where(device => device.Started).Any(device => device.Started);

    public static bool StartNetworkCapture(TrackingController trackingController)
    {
        ReceiverBuilder builder = ReceiverBuilder.Create();

        builder.AddEventHandler(new NewEquipmentItemEventHandler(trackingController));
        builder.AddEventHandler(new NewSimpleItemEventHandler(trackingController));
        builder.AddEventHandler(new NewFurnitureItemEventHandler(trackingController));
        builder.AddEventHandler(new NewJournalItemEventHandler(trackingController));
        builder.AddEventHandler(new NewLaborerItemEventHandler(trackingController));
        builder.AddEventHandler(new OtherGrabbedLootEventHandler(trackingController));
        builder.AddEventHandler(new InventoryDeleteItemEventHandler(trackingController));
        //builder.AddEventHandler(new InventoryPutItemEventHandler(trackingController));
        builder.AddEventHandler(new TakeSilverEventHandler(trackingController));
        builder.AddEventHandler(new ActionOnBuildingFinishedEventHandler(trackingController));
        builder.AddEventHandler(new UpdateFameEventHandler(trackingController));
        builder.AddEventHandler(new UpdateSilverEventHandler(trackingController));
        builder.AddEventHandler(new UpdateReSpecPointsEventHandler(trackingController));
        builder.AddEventHandler(new UpdateCurrencyEventHandler(trackingController));
        builder.AddEventHandler(new DiedEventHandler(trackingController));
        builder.AddEventHandler(new NewLootChestEventHandler(trackingController));
        builder.AddEventHandler(new UpdateLootChestEventHandler(trackingController));
        builder.AddEventHandler(new LootChestOpenedEventHandler(trackingController));
        builder.AddEventHandler(new InCombatStateUpdateEventHandler(trackingController));
        builder.AddEventHandler(new NewShrineEventHandler(trackingController));
        builder.AddEventHandler(new HealthUpdateEventHandler(trackingController));
        builder.AddEventHandler(new PartyDisbandedEventHandler(trackingController));
        builder.AddEventHandler(new PartyPlayerJoinedEventHandler(trackingController));
        builder.AddEventHandler(new PartyPlayerLeftEventHandler(trackingController));
        builder.AddEventHandler(new PartyChangedOrderEventHandler(trackingController));
        builder.AddEventHandler(new NewCharacterEventHandler(trackingController));
        builder.AddEventHandler(new SiegeCampClaimStartEventHandler(trackingController));
        builder.AddEventHandler(new CharacterEquipmentChangedEventHandler(trackingController));
        builder.AddEventHandler(new NewMobEventHandler(trackingController));
        builder.AddEventHandler(new ActiveSpellEffectsUpdateEventHandler(trackingController));
        builder.AddEventHandler(new UpdateFactionStandingEventHandler(trackingController));
        //builder.AddEventHandler(new ReceivedSeasonPointsEventHandler(trackingController));
        builder.AddEventHandler(new MightFavorPointsEventHandler(trackingController));
        builder.AddEventHandler(new BaseVaultInfoEventHandler(trackingController));
        builder.AddEventHandler(new GuildVaultInfoEventHandler(trackingController));
        builder.AddEventHandler(new NewLootEventHandler(trackingController));
        builder.AddEventHandler(new AttachItemContainerEventHandler(trackingController));
        builder.AddEventHandler(new HarvestFinishedEventHandler(trackingController));

        builder.AddRequestHandler(new InventoryMoveItemRequestHandler(trackingController));
        builder.AddRequestHandler(new UseShrineRequestHandler(trackingController));
        builder.AddRequestHandler(new ReSpecBoostRequestHandler(trackingController));
        builder.AddRequestHandler(new TakeSilverRequestHandler(trackingController));
        builder.AddRequestHandler(new RegisterToObjectRequestHandler(trackingController));
        builder.AddRequestHandler(new UnRegisterFromObjectRequestHandler(trackingController));
        builder.AddRequestHandler(new AuctionBuyOfferRequestHandler(trackingController));
        builder.AddRequestHandler(new AuctionSellSpecificItemRequestHandler(trackingController));

        builder.AddResponseHandler(new ChangeClusterResponseHandler(trackingController));
        builder.AddResponseHandler(new PartyMakeLeaderResponseHandler(trackingController));
        builder.AddResponseHandler(new JoinResponseHandler(trackingController));
        builder.AddResponseHandler(new GetMailInfosResponseHandler(trackingController));
        builder.AddResponseHandler(new ReadMailResponseHandler(trackingController));
        builder.AddResponseHandler(new AuctionGetOffersResponseHandler(trackingController));
        builder.AddResponseHandler(new AuctionGetResponseHandler(trackingController));

        _receiver = builder.Build();

        try
        {
            CapturedDevices.AddRange(CaptureDeviceList.Instance);
            return StartDeviceCapture();
        }
        catch (Exception e)
        {
            ConsoleManager.WriteLineForError(MethodBase.GetCurrentMethod()?.DeclaringType, e);
            Log.Error(MethodBase.GetCurrentMethod()?.DeclaringType, e);

            var mainWindowViewModel = ServiceLocator.Resolve<MainWindowViewModel>();
            if (mainWindowViewModel != null)
            {
                mainWindowViewModel.SetErrorBar(Visibility.Visible, LanguageController.Translation("PACKET_HANDLER_ERROR_MESSAGE"));
                _ = mainWindowViewModel.StopTrackingAsync();
            }
            else
            {
                Log.Error(MethodBase.GetCurrentMethod()?.DeclaringType + " - MainWindowViewModel is null.");
            }

            return false;
        }
    }

    private static bool StartDeviceCapture()
    {
        if (CapturedDevices.Count <= 0)
        {
            return false;
        }

        try
        {
            foreach (var device in CapturedDevices)
            {
                PacketEvent(device);
            }
        }
        catch (Exception e)
        {
            ConsoleManager.WriteLineForError(MethodBase.GetCurrentMethod()?.DeclaringType, e);
            Log.Error(MethodBase.GetCurrentMethod()?.DeclaringType, e);

            var mainWindowViewModel = ServiceLocator.Resolve<MainWindowViewModel>();
            if (mainWindowViewModel != null)
            {
                mainWindowViewModel.SetErrorBar(Visibility.Visible, LanguageController.Translation("PACKET_HANDLER_ERROR_MESSAGE"));
                _ = mainWindowViewModel.StopTrackingAsync();
            }
            else
            {
                Log.Error(MethodBase.GetCurrentMethod()?.DeclaringType + " - MainWindowViewModel is null.");
            }

            return false;
        }

        return true;
    }

    public static void StopNetworkCapture()
    {
        foreach (var device in CapturedDevices.Where(device => device.Started))
        {
            device.StopCapture();
            device.Close();
        }

        CapturedDevices.Clear();
    }



    private static void PacketEvent(ICaptureDevice device)
    {
        if (!device.Started)
        {
            device.Open(new DeviceConfiguration()
            {
                Mode = DeviceModes.DataTransferUdp | DeviceModes.Promiscuous | DeviceModes.NoCaptureLocal,
                ReadTimeout = 5000
            });

            device.Filter = "(src host 5.45.187 or host 5.188.125) and udp port 5056";
            device.OnPacketArrival += Device_OnPacketArrival;
            device.StartCapture();
        }
    }

    private static void Device_OnPacketArrival(object sender, PacketCapture e)
    {
        try
        {
            var server = GetCurrentServerByIp(e);
            _ = UpdateMainWindowServerTypeAsync(server);
            AlbionServer = server;
            if (server == AlbionServer.Unknown)
            {
                return;
            }

            var packet = Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data).Extract<UdpPacket>();
            if (packet != null)
            {
                _receiver.ReceivePacket(packet.PayloadData);
            }
        }
        catch (IndexOutOfRangeException ex)
        {
            ConsoleManager.WriteLineForWarning(MethodBase.GetCurrentMethod()?.DeclaringType, ex);
        }
        catch (InvalidOperationException ex)
        {
            ConsoleManager.WriteLineForError(MethodBase.GetCurrentMethod()?.DeclaringType, ex);
        }
        catch (OverflowException ex)
        {
            ConsoleManager.WriteLineForError(MethodBase.GetCurrentMethod()?.DeclaringType, ex);
        }
        catch (ArgumentException ex)
        {
            ConsoleManager.WriteLineForWarning(MethodBase.GetCurrentMethod()?.DeclaringType, ex);
        }
        catch (Exception ex)
        {
            ConsoleManager.WriteLineForError(MethodBase.GetCurrentMethod()?.DeclaringType, ex);
            Log.Error(nameof(Device_OnPacketArrival), ex);
        }
    }

    private static AlbionServer GetCurrentServerByIp(PacketCapture e)
    {
        if (SettingsController.CurrentSettings.Server == 1)
        {
            return AlbionServer.West;
        }

        if (SettingsController.CurrentSettings.Server == 2)
        {
            return AlbionServer.East;
        }

        var packet = Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data);
        var ipPacket = packet.Extract<IPPacket>();
        var srcIp = ipPacket?.SourceAddress?.ToString();
        var albionServer = AlbionServer.Unknown;

        if (srcIp == null || string.IsNullOrEmpty(srcIp))
        {
            albionServer = AlbionServer.Unknown;
        }
        else if (srcIp.Contains("5.188.125."))
        {
            albionServer = AlbionServer.West;
        }
        else if (srcIp!.Contains("5.45.187."))
        {
            albionServer = AlbionServer.East;
        }

        return GetActiveAlbionServer(albionServer);
    }

    private static AlbionServer GetActiveAlbionServer(AlbionServer albionServer)
    {
        if (albionServer != AlbionServer.Unknown && _lastServerType == albionServer)
        {
            _serverEventCounter++;
        }
        else if (albionServer != AlbionServer.Unknown)
        {
            _serverEventCounter = 1;
            _lastServerType = albionServer;
        }

        if (_serverEventCounter < 20 || albionServer == AlbionServer.Unknown)
        {
            return _lastServerType;
        }

        _serverEventCounter = 20;
        return albionServer;
    }

    private static async Task UpdateMainWindowServerTypeAsync(AlbionServer albionServer)
    {
        if ((DateTime.Now - _lastGetCurrentServerByIpTime).TotalSeconds < 10)
        {
            return;
        }

        await Task.Run(() =>
        {
            var mainWindowViewModel = ServiceLocator.Resolve<MainWindowViewModel>();
            mainWindowViewModel.ServerTypeText = albionServer switch
            {
                AlbionServer.East => LanguageController.Translation("EAST_SERVER"),
                AlbionServer.West => LanguageController.Translation("WEST_SERVER"),
                _ => LanguageController.Translation("UNKNOWN_SERVER")
            };
        });

        _lastGetCurrentServerByIpTime = DateTime.Now;
    }
}