import 'dart:async';
import 'package:flutter/material.dart';
import 'package:webview_flutter/webview_flutter.dart';
import 'package:url_launcher/url_launcher.dart';
import '../services/subscription_service.dart';

class PaymentWebViewScreen extends StatefulWidget {
  final String paymentUrl;
  final String paymentId;

  const PaymentWebViewScreen({
    super.key,
    required this.paymentUrl,
    required this.paymentId,
  });

  @override
  State<PaymentWebViewScreen> createState() => _PaymentWebViewScreenState();
}

class _PaymentWebViewScreenState extends State<PaymentWebViewScreen> {
  late final WebViewController _controller;
  final SubscriptionService _subService = SubscriptionService();
  Timer? _pollingTimer;
  bool _isLoading = true;
  double _progress = 0;

  @override
  void initState() {
    super.initState();

    _controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setBackgroundColor(Colors.white)
      ..setNavigationDelegate(
        NavigationDelegate(
          onProgress: (int progress) {
            if (mounted) {
              setState(() {
                _progress = progress / 100.0;
                _isLoading = progress < 100;
              });
            }
          },
          onPageStarted: (String url) {
            if (mounted) setState(() => _isLoading = true);
          },
          onPageFinished: (String url) {
            if (mounted) setState(() => _isLoading = false);
          },
          onWebResourceError: (WebResourceError error) {
            debugPrint('Payment WebView error: ${error.description}');
          },
          onNavigationRequest: (NavigationRequest request) {
            final url = request.url;
            if (url.startsWith('http://') || url.startsWith('https://')) {
              return NavigationDecision.navigate;
            }
            try {
              final uri = Uri.parse(url);
              launchUrl(uri, mode: LaunchMode.externalApplication);
            } catch (e) {
              debugPrint('Could not launch external url: $url, error: $e');
            }
            return NavigationDecision.prevent;
          },
        ),
      )
      ..loadRequest(Uri.parse(widget.paymentUrl));

    _startPolling();
  }

  void _startPolling() {
    _pollingTimer?.cancel();
    int attempts = 0;
    const maxAttempts = 300; // 15 دقيقة كحد أقصى

    _pollingTimer = Timer.periodic(const Duration(seconds: 3), (timer) async {
      attempts++;
      if (attempts > maxAttempts) {
        timer.cancel();
        return;
      }

      final status = await _subService.getPaymentStatus(widget.paymentId);
      if (!mounted) return;

      if (status == 'Settled') {
        timer.cancel();
        Navigator.of(context).pop(true);
      } else if (status == 'Cancelled' || status == 'Rejected') {
        timer.cancel();
        Navigator.of(context).pop(false);
      }
    });
  }

  @override
  void dispose() {
    _pollingTimer?.cancel();
    super.dispose();
  }

  Future<bool> _onWillPop() async {
    final shouldLeave = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Text('إلغاء عملية الدفع؟', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16), textAlign: TextAlign.right),
        content: const Text('هل أنت متأكد من رغبتك في إغلاق صفحة الدفع والعودة؟', textAlign: TextAlign.right),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('متابعة الدفع', style: TextStyle(fontWeight: FontWeight.bold)),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: Colors.red.shade600,
              foregroundColor: Colors.white,
            ),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('إلغاء والعودة'),
          ),
        ],
      ),
    );
    return shouldLeave ?? false;
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: PopScope(
        canPop: false,
        onPopInvokedWithResult: (didPop, result) async {
          if (didPop) return;
          final leave = await _onWillPop();
          if (leave && mounted) {
            Navigator.of(context).pop(false);
          }
        },
        child: Scaffold(
          backgroundColor: Colors.white,
          appBar: AppBar(
            title: const Text(
              'الدفع الإلكتروني الآمن',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 17),
            ),
            centerTitle: true,
            backgroundColor: Colors.white,
            elevation: 0.5,
            leading: IconButton(
              icon: const Icon(Icons.close, color: Color(0xFF0F172A)),
              onPressed: () async {
                final leave = await _onWillPop();
                if (leave && mounted) {
                  Navigator.of(context).pop(false);
                }
              },
            ),
            bottom: _isLoading
                ? PreferredSize(
                    preferredSize: const Size.fromHeight(3),
                    child: LinearProgressIndicator(
                      value: _progress > 0 ? _progress : null,
                      backgroundColor: Colors.transparent,
                      valueColor: const AlwaysStoppedAnimation<Color>(Color(0xFF0284C7)),
                      minHeight: 3,
                    ),
                  )
                : null,
          ),
          body: WebViewWidget(controller: _controller),
        ),
      ),
    );
  }
}
