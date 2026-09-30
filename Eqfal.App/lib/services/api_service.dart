import 'package:dio/dio.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../models/operation.dart';
import '../config.dart';

class ApiService {
  final Dio _dio;

  ApiService() : _dio = Dio(BaseOptions(
    baseUrl: Config.apiUrl,
    connectTimeout: const Duration(seconds: 5),
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
      onError: (DioException e, handler) async {
        if (e.response?.statusCode == 401) {
          print('🚨 401 Error on Path: ${e.requestOptions.path}');
        }
        return handler.next(e);
      },
    ));
  }

  Future<bool> login(String username, String password) async {
    try {
      final response = await _dio.post('/Auth/login', data: {
        'username': username,
        'password': password,
      });

      if (response.statusCode == 200 && response.data['token'] != null) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('jwt_token', response.data['token']);
        return true;
      }
      return false;
    } on DioException catch (e) {
      if (e.response != null && e.response?.data != null) {
        throw Exception(e.response?.data['message'] ?? 'فشل تسجيل الدخول');
      }
      throw Exception('فشل الاتصال بالخادم');
    } catch (e) {
      throw Exception('حدث خطأ غير متوقع');
    }
  }

  Future<bool> register(String fullName, String phone, String email, String password) async {
    try {
      final response = await _dio.post('/Auth/register', data: {
        'fullName': fullName,
        'phone': phone,
        'email': email,
        'usernameEmail': email.isNotEmpty ? email : phone,
        'password': password,
      });

      if (response.statusCode == 200 && response.data['token'] != null) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('jwt_token', response.data['token']);
        return true;
      }
      return false;
    } on DioException catch (e) {
      if (e.response != null && e.response?.data != null) {
        throw Exception(e.response?.data['message'] ?? 'فشل إنشاء الحساب');
      }
      throw Exception('فشل الاتصال بالخادم');
    } catch (e) {
      throw Exception('حدث خطأ غير متوقع');
    }
  }

  Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('jwt_token');
  }

  Future<bool> updateFcmToken(String token) async {
    try {
      final response = await _dio.post('/Auth/update-fcm-token', data: {
        'fcmToken': token,
      });
      return response.statusCode == 200;
    } catch (e) {
      print('Error updating FCM token: $e');
      return false;
    }
  }

  Future<bool> forgotPassword(String phone) async {
    try {
      final response = await _dio.post('/Auth/forgot-password', data: {
        'phone': phone,
      });
      return response.statusCode == 200;
    } catch (e) {
      throw Exception('فشل الاتصال بالخادم أو الرقم غير صحيح');
    }
  }

  Future<bool> verifyOtp(String phone, String otp) async {
    try {
      final response = await _dio.post('/Auth/verify-otp', data: {
        'phone': phone,
        'otp': otp,
      });
      return response.statusCode == 200;
    } on DioException catch (e) {
      if (e.response != null && e.response?.data != null) {
        throw Exception(e.response?.data['message'] ?? 'الكود غير صحيح');
      }
      throw Exception('فشل الاتصال بالخادم');
    } catch (e) {
      throw Exception('حدث خطأ غير متوقع');
    }
  }

  Future<bool> resetPassword(String phone, String otp, String newPassword) async {
    try {
      final response = await _dio.post('/Auth/reset-password', data: {
        'phone': phone,
        'otp': otp,
        'newPassword': newPassword,
      });
      return response.statusCode == 200;
    } on DioException catch (e) {
      if (e.response != null && e.response?.data != null) {
        throw Exception(e.response?.data['message'] ?? 'فشل تغيير كلمة المرور');
      }
      throw Exception('فشل الاتصال بالخادم');
    } catch (e) {
      throw Exception('حدث خطأ غير متوقع');
    }
  }

  Future<List<Operation>> getOperations() async {
    try {
      final response = await _dio.get('/Operations');
      if (response.statusCode == 200) {
        List<dynamic> data = response.data;
        return data.map((json) => Operation.fromJson(json)).toList();
      }
      return [];
    } catch (e) {
      print('Error fetching operations: $e');
      return [];
    }
  }

  Future<Operation?> getOperation(int id) async {
    try {
      final response = await _dio.get('/Operations/$id');
      if (response.statusCode == 200) {
        return Operation.fromJson(response.data);
      }
      return null;
    } catch (e) {
      print('Error fetching operation: $e');
      return null;
    }
  }

  Future<bool> updateOperation(Operation operation) async {
    try {
      Response response;
      try {
        response = await _dio.put(
          '/Operations/${operation.id}',
          data: operation.toJson(),
        );
      } catch (_) {
        // Fallback to POST /Operations/{id}/update if PUT is blocked by IIS WebDAV
        response = await _dio.post(
          '/Operations/${operation.id}/update',
          data: operation.toJson(),
        );
      }
      return response.statusCode == 204 || response.statusCode == 200;
    } catch (e) {
      print('Error updating operation: $e');
      return false;
    }
  }

  Future<bool> deleteOperation(int id) async {
    try {
      Response response;
      try {
        response = await _dio.delete('/Operations/$id');
      } catch (_) {
        response = await _dio.post('/Operations/$id/delete');
      }
      return response.statusCode == 204 || response.statusCode == 200;
    } catch (e) {
      print('Error deleting operation: $e');
      return false;
    }
  }

  Future<bool> batchDeleteOperations(List<int> ids) async {
    if (ids.isEmpty) return true;
    try {
      final response = await _dio.post(
        '/Operations/batch-delete',
        data: {'ids': ids},
      );
      return response.statusCode == 200 || response.statusCode == 204;
    } catch (e) {
      print('Error batch deleting operations: $e');
      return false;
    }
  }

  Future<bool> restoreOperation(int id) async {
    try {
      final response = await _dio.post('/Operations/$id/restore');
      return response.statusCode == 200;
    } catch (e) {
      print('Error restoring operation: $e');
      return false;
    }
  }

  Future<int?> createOperation(Operation operation) async {
    try {
      final response = await _dio.post(
        '/Operations',
        data: operation.toJson(),
      );
      if (response.statusCode == 201 || response.statusCode == 200) {
        return response.data['id'];
      }
      return null;
    } catch (e) {
      print('Error creating operation: $e');
      return null;
    }
  }

  // Keywords Methods
  Future<List<dynamic>> getKeywords(String type) async {
    try {
      final response = await _dio.get('/Keywords?type=$type');
      if (response.statusCode == 200) {
        return response.data;
      }
      return [];
    } catch (e) {
      print('Error fetching keywords: $e');
      return [];
    }
  }

  Future<bool> addKeyword(String word, String type) async {
    try {
      final response = await _dio.post('/Keywords', data: {
        'word': word,
        'type': type,
      });
      return response.statusCode == 201 || response.statusCode == 200;
    } catch (e) {
      print('Error adding keyword: $e');
      return false;
    }
  }

  Future<bool> deleteKeyword(int id) async {
    try {
      Response response;
      try {
        response = await _dio.post('/Keywords/$id/delete');
      } catch (_) {
        response = await _dio.delete('/Keywords/$id');
      }
      return response.statusCode == 204 || response.statusCode == 200;
    } catch (e) {
      print('Error deleting keyword: $e');
      return false;
    }
  }

  Future<bool> updateKeyword(int id, String word, String type) async {
    try {
      Response response;
      try {
        response = await _dio.post('/Keywords/$id/update', data: {
          'id': id,
          'word': word,
          'type': type,
        });
      } catch (_) {
        response = await _dio.put('/Keywords/$id', data: {
          'id': id,
          'word': word,
          'type': type,
        });
      }
      return response.statusCode == 200 || response.statusCode == 204;
    } catch (e) {
      print('Error updating keyword: $e');
      return false;
    }
  }

  Future<bool> renameCurrencyType(String oldType, String newType) async {
    try {
      final response = await _dio.post('/Keywords/rename-type', data: {
        'oldType': oldType,
        'newType': newType,
      });
      return response.statusCode == 200 || response.statusCode == 204;
    } catch (e) {
      print('Error renaming currency type: $e');
      return false;
    }
  }

  Future<bool> deleteCurrencyType(String type) async {
    try {
      Response response;
      try {
        response = await _dio.post('/Keywords/delete-type', data: {
          'type': type,
        });
      } catch (_) {
        response = await _dio.delete('/Keywords/type/$type');
      }
      return response.statusCode == 200 || response.statusCode == 204;
    } catch (e) {
      print('Error deleting currency type: $e');
      return false;
    }
  }

  Future<bool> syncDefaultCurrencies() async {
    try {
      final response = await _dio.post('/Keywords/sync-currencies');
      return response.statusCode == 200;
    } catch (e) {
      print('Error syncing default currencies: $e');
      return false;
    }
  }

  // Manual Analysis Method
  Future<Map<String, dynamic>?> analyzeMessage(String text) async {
    try {
      final response = await _dio.post('/Operations/analyze', data: {
        'text': text,
      });
      if (response.statusCode == 200) {
        return response.data;
      }
      return null;
    } catch (e) {
      print('Error analyzing message: $e');
      return null;
    }
  }

  // Monitored Numbers Endpoints
  Future<List<dynamic>> getMonitoredNumbers() async {
    final response = await _dio.get('/MonitoredNumbers');
    return response.data;
  }

  Future<dynamic> addMonitoredNumber(String phoneNumber, String contactName) async {
    try {
      final response = await _dio.post('/MonitoredNumbers', data: {
        'phoneNumber': phoneNumber,
        'contactName': contactName,
      });
      return response.data;
    } catch (e) {
      if (e is DioException && e.response != null) {
        throw Exception(e.response?.data['message'] ?? 'فشل في إضافة الرقم');
      }
      throw Exception('فشل الاتصال بالخادم');
    }
  }

  Future<dynamic> toggleMonitoredNumberStatus(int id, {bool? isActive}) async {
    try {
      final response = await _dio.post(
        '/MonitoredNumbers/$id/update',
        data: {'isActive': isActive},
      );
      return response.data;
    } catch (e) {
      try {
        final res2 = await _dio.post('/MonitoredNumbers/$id/toggle');
        return res2.data;
      } catch (_) {}
      print('Error toggling monitored number: $e');
      throw Exception('فشل في تحديث حالة الرقم');
    }
  }

  Future<dynamic> editMonitoredNumber(int id, String contactName) async {
    try {
      final response = await _dio.post('/MonitoredNumbers/$id/edit', data: {
        'contactName': contactName,
        'phoneNumber': '',
      });
      return response.data;
    } catch (e) {
      print('Error editing monitored number: $e');
      throw Exception('فشل في تعديل اسم الرقم');
    }
  }

  Future<bool> deleteMonitoredNumber(int id) async {
    try {
      final response = await _dio.post('/MonitoredNumbers/$id/delete');
      return response.statusCode == 204 || response.statusCode == 200;
    } catch (e) {
      print('Error deleting monitored number: $e');
      throw Exception('فشل في حذف الرقم');
    }
  }

  Future<Map<String, dynamic>?> getUserProfile() async {
    try {
      final response = await _dio.get('/Auth/profile');
      if (response.statusCode == 200 && response.data != null) {
        return Map<String, dynamic>.from(response.data);
      }
    } catch (e) {
      print('Error getting user profile: $e');
    }
    return null;
  }

  Future<Map<String, dynamic>> updateUserProfile({
    required String fullName,
    required String phone,
    required String usernameEmail,
  }) async {
    try {
      final response = await _dio.post('/Auth/profile', data: {
        'fullName': fullName,
        'phone': phone,
        'usernameEmail': usernameEmail,
      });
      if (response.statusCode == 200) {
        return {
          'success': true,
          'message': response.data['message'] ?? 'تم حفظ التعديلات بنجاح',
          'user': response.data['user']
        };
      }
      return {'success': false, 'message': 'فشل في حفظ التعديلات'};
    } on DioException catch (e) {
      if (e.response != null && e.response?.data != null && e.response?.data['message'] != null) {
        return {'success': false, 'message': e.response?.data['message']};
      }
      return {'success': false, 'message': 'فشل الاتصال بالخادم'};
    } catch (e) {
      return {'success': false, 'message': 'حدث خطأ غير متوقع: $e'};
    }
  }

  Future<Map<String, dynamic>> deleteAccount() async {
    try {
      final response = await _dio.delete('/Auth/delete-account');
      if (response.statusCode == 200 || response.statusCode == 204) {
        return {'success': true};
      }
      return {'success': false, 'message': 'فشل حذف الحساب'};
    } on DioException catch (e) {
      final msg = (e.response?.data is Map) ? (e.response?.data['message'] ?? 'فشل حذف الحساب') : 'تعذر الاتصال بالخادم';
      return {'success': false, 'message': msg};
    } catch (e) {
      return {'success': false, 'message': 'حدث خطأ أثناء حذف الحساب'};
    }
  }

  Future<Map<String, dynamic>> getWhatsAppStatus() async {
    try {
      final response = await _dio.get('/WhatsApp/status');
      if (response.statusCode == 200 && response.data != null) {
        return Map<String, dynamic>.from(response.data);
      }
    } catch (e) {
      print('Error getting WhatsApp status: $e');
    }
    return {'connected': false, 'status': 'غير متصل'};
  }

  Future<Map<String, dynamic>> requestWhatsAppPairingCode({
    required String phone,
    bool forceReset = false,
  }) async {
    try {
      final response = await _dio.post(
        '/WhatsApp/pair',
        data: {
          'phone': phone,
          'forceReset': forceReset,
        },
        options: Options(
          receiveTimeout: const Duration(seconds: 35),
          sendTimeout: const Duration(seconds: 15),
        ),
      );
      if (response.statusCode == 200 && response.data != null) {
        return Map<String, dynamic>.from(response.data);
      }
      return {'success': false, 'message': 'فشل في استلام رمز الربط'};
    } on DioException catch (e) {
      if (e.type == DioExceptionType.receiveTimeout || e.type == DioExceptionType.connectionTimeout) {
        return {
          'success': false,
          'message': 'استغرق خادم واتساب وقتاً أطول من المعتاد للاستجابة. يرجى إعادة المحاولة.',
        };
      }
      final msg = (e.response?.data is Map)
          ? (e.response?.data['message'] ?? 'فشل طلب رمز الربط')
          : 'تعذر الاتصال بالخادم';
      return {'success': false, 'message': msg};
    } catch (e) {
      return {'success': false, 'message': 'حدث خطأ غير متوقع: $e'};
    }
  }

  Future<bool> disconnectWhatsApp() async {
    try {
      final response = await _dio.post('/WhatsApp/disconnect');
      return response.statusCode == 200;
    } catch (e) {
      print('Error disconnecting WhatsApp: $e');
      return false;
    }
  }

  Future<Map<String, dynamic>> submitSupportTicket({
    required String fullName,
    required String phoneNumber,
    String? email,
    String? subject,
    required String message,
  }) async {
    try {
      final response = await _dio.post(
        '/Support/ticket',
        data: {
          'fullName': fullName,
          'phoneNumber': phoneNumber,
          'email': email,
          'subject': subject,
          'message': message,
        },
      );
      if (response.statusCode == 200 && response.data != null) {
        return Map<String, dynamic>.from(response.data);
      }
      return {'success': false, 'message': 'فشل إرسال طلب الدعم'};
    } on DioException catch (e) {
      final msg = (e.response?.data is Map)
          ? (e.response?.data['message'] ?? 'فشل إرسال طلب الدعم')
          : 'تعذر الاتصال بالخادم، يرجى المحاولة لاحقاً';
      return {'success': false, 'message': msg};
    } catch (e) {
      return {'success': false, 'message': 'حدث خطأ غير متوقع: $e'};
    }
  }
}
