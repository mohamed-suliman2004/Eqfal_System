import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:signalr_netcore/signalr_client.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'dart:convert';
import 'dart:async';
import '../providers/operations_provider.dart';
import '../providers/monitored_numbers_provider.dart';
import '../config.dart';

final signalRServiceProvider = Provider<SignalRService>((ref) {
  return SignalRService(ref);
});

class SignalRService {
  final Ref _ref;
  HubConnection? _hubConnection;
  bool _isConnecting = false;
  Timer? _healthTimer;

  SignalRService(this._ref) {
    _startHealthCheck();
  }

  void _startHealthCheck() {
    _healthTimer?.cancel();
    _healthTimer = Timer.periodic(const Duration(seconds: 8), (timer) {
      if (_hubConnection == null || _hubConnection?.state != HubConnectionState.Connected) {
        initializeConnection();
      }
    });
  }

  Future<void> initializeConnection() async {
    if (_isConnecting) return;
    if (_hubConnection != null && _hubConnection?.state == HubConnectionState.Connected) {
      return;
    }
    _isConnecting = true;

    if (_hubConnection != null) {
      try {
        await _hubConnection?.stop();
      } catch (_) {}
    }
    
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString('jwt_token') ?? '';
    if (token.isEmpty) {
      _isConnecting = false;
      return;
    }

    final connectionUrl = "${Config.signalRUrl}?access_token=$token";

    _hubConnection = HubConnectionBuilder()
        .withUrl(
          connectionUrl,
          options: HttpConnectionOptions(
            accessTokenFactory: () async {
              final p = await SharedPreferences.getInstance();
              return p.getString('jwt_token') ?? '';
            },
          ),
        )
        .withAutomaticReconnect(
          retryDelays: [0, 1000, 2000, 3000, 5000, 10000],
        )
        .build();

    _hubConnection?.onclose(({error}) {
      Future.delayed(const Duration(seconds: 2), () {
        initializeConnection();
      });
    });

    _hubConnection?.onreconnected(({connectionId}) async {
      await _joinGroupAfterConnect(token);
      _triggerRefresh();
    });

    // 1. New Operation Broadcast
    _hubConnection?.on("ReceiveNewOperation", (arguments) {
      _triggerRefresh();
    });

    // 2. Monitored Numbers Broadcast
    _hubConnection?.on("MonitoredNumbersChanged", (arguments) {
      _triggerRefresh();
    });

    // 3. Audit Log Broadcast
    _hubConnection?.on("ReceiveAuditLog", (arguments) {
      _triggerRefresh();
    });

    // 4. WhatsApp Status Changed
    _hubConnection?.on("WhatsAppStatusChanged", (arguments) {
      _triggerRefresh();
    });

    // 5. Operations Batch Deleted
    _hubConnection?.on("ReceiveOperationsBatchDeleted", (arguments) {
      _triggerRefresh();
    });

    try {
      await _hubConnection?.start();
      await _joinGroupAfterConnect(token);
      _triggerRefresh();
    } catch (e) {
      print("SignalR Connection Error: $e");
    } finally {
      _isConnecting = false;
    }
  }

  void _triggerRefresh() {
    try {
      _ref.invalidate(operationsProvider);
      _ref.refresh(operationsProvider);
    } catch (_) {}

    try {
      _ref.invalidate(monitoredNumbersProvider);
      _ref.read(monitoredNumbersProvider.notifier).fetchNumbers();
    } catch (_) {}
  }

  Future<void> _joinGroupAfterConnect(String token) async {
    try {
      final parts = token.split('.');
      if (parts.length > 1) {
        final normalized = base64Url.normalize(parts[1]);
        final decoded = utf8.decode(base64Url.decode(normalized));
        final payload = jsonDecode(decoded);
        final userIdStr = payload['nameid'] ?? payload['sub'] ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
        if (userIdStr != null && int.tryParse(userIdStr.toString()) != null) {
          final userId = int.parse(userIdStr.toString());
          await _hubConnection?.invoke("JoinUserGroup", args: [userId]);
        }
      }
    } catch (_) {}
  }

  Future<void> stopConnection() async {
    _healthTimer?.cancel();
    await _hubConnection?.stop();
    _hubConnection = null;
  }
}