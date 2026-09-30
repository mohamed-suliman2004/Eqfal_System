import 'package:dio/dio.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../config.dart';
import '../models/subscription_models.dart';

class SubscriptionService {
  final Dio _dio;

  SubscriptionService() : _dio = Dio(BaseOptions(
    baseUrl: Config.apiUrl,
    connectTimeout: const Duration(seconds: 10),
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    },
  )) {
    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final prefs = await SharedPreferences.getInstance();
        final token = prefs.getString('jwt_token');
        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
          options.headers['X-Authorization'] = 'Bearer $token';
        }
        return handler.next(options);
      },
    ));
  }

  /// استعلام اشتراك المستخدم الحالي
  Future<SubscriptionStatus> getCurrentSubscription() async {
    try {
      final res = await _dio.get('/subscriptions/current');
      if (res.statusCode == 200 && res.data != null) {
        return SubscriptionStatus.fromJson(res.data);
      }
      throw Exception('فشل جلب بيانات الاشتراك');
    } catch (e) {
      print('[SubscriptionService] Error getting current subscription: $e');
      return SubscriptionStatus(
        status: 'None',
        planType: 'غير محدد',
        isTrial: false,
        daysRemaining: 0,
      );
    }
  }

  /// جلب كتالوج الخطط المتاحة
  Future<List<SubscriptionPlanItem>> getActiveCatalog() async {
    try {
      final res = await _dio.get('/subscription-plans/active');
      if (res.statusCode == 200 && res.data != null && res.data['plans'] != null) {
        final list = res.data['plans'] as List<dynamic>;
        return list.map((p) => SubscriptionPlanItem.fromJson(p)).toList();
      }
      return [];
    } catch (e) {
      print('[SubscriptionService] Error getting active catalog: $e');
      return [];
    }
  }

  /// معاينة أثر تغيير الخطة (قاعدة D6)
  Future<PlanChangePreview> previewPlanChange(String planPriceId) async {
    try {
      final res = await _dio.get('/subscription-payments/plan-change-preview', queryParameters: {
        'planPriceId': planPriceId,
      });

      if (res.statusCode == 200 && res.data != null) {
        return PlanChangePreview.fromJson(res.data);
      }
      throw Exception(res.data?['message'] ?? 'فشل معاينة الخطة');
    } on DioException catch (e) {
      throw Exception(e.response?.data?['message'] ?? 'فشل معاينة الخطة');
    }
  }

  /// التحقق من كود الخصم
  Future<DiscountValidationResult> validateDiscount(String planPriceId, String code) async {
    try {
      final res = await _dio.post('/subscription-payments/validate-discount', data: {
        'planPriceId': planPriceId,
        'code': code.trim(),
      });

      if (res.statusCode == 200 && res.data != null) {
        return DiscountValidationResult.fromJson(res.data);
      }
      throw Exception(res.data?['message'] ?? 'فشل فحص كود الخصم');
    } on DioException catch (e) {
      throw Exception(e.response?.data?['message'] ?? 'كود الخصم غير صالح');
    }
  }

  /// إنشاء عملية الدفع والحصول على رابط EzonePay
  Future<Map<String, dynamic>> checkout({
    required String planPriceId,
    String? discountCode,
    bool confirmReplaceActivePlan = false,
  }) async {
    try {
      final res = await _dio.post('/subscription-payments/checkout', data: {
        'planPriceId': planPriceId,
        'discountCode': discountCode?.trim(),
        'confirmReplaceActivePlan': confirmReplaceActivePlan,
      });

      if (res.statusCode == 200 && res.data != null) {
        return {
          'id': res.data['id']?.toString(),
          'paymentUrl': res.data['paymentUrl']?.toString(),
          'isFree': res.data['isFree'] == true,
          'message': res.data['message'],
        };
      }
      throw Exception(res.data?['message'] ?? 'فشل إنشاء رابط الدفع');
    } on DioException catch (e) {
      final errorData = e.response?.data;
      if (e.response?.statusCode == 409 && errorData?['errorCode'] == 'PLAN_CHANGE_CONFIRMATION_REQUIRED') {
        throw PlanChangeRequiredException(errorData?['message'] ?? 'يلزم تأكيد تغيير الخطة');
      }
      throw Exception(errorData?['message'] ?? 'تعذر الاتصال ببوابة الدفع');
    }
  }

  /// الاستعلام عن حالة الدفعة (Polling)
  Future<String> getPaymentStatus(String paymentId) async {
    try {
      final res = await _dio.get('/subscription-payments/$paymentId/status');
      if (res.statusCode == 200 && res.data != null) {
        return res.data['status']?.toString() ?? 'Pending';
      }
      return 'Pending';
    } catch (e) {
      return 'Pending';
    }
  }
}

class PlanChangeRequiredException implements Exception {
  final String message;
  PlanChangeRequiredException(this.message);
  @override
  String toString() => message;
}
