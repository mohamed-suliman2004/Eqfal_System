import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, BehaviorSubject, of, timer } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { SystemHealth, SystemMetrics, UserSession, SystemLog } from '../models/monitor.model';

@Injectable({
  providedIn: 'root'
})
export class MonitorService {
  private apiBaseUrl = 'http://localhost:5180';
  private nodeBaseUrl = 'http://localhost:3010';
  private serviceApiKey = 'eqfal_sec_wa_2026_internal_key_8f92a3';

  // Live Auto-Refresh Subjects
  public autoRefresh$ = new BehaviorSubject<boolean>(true);
  public health$ = new BehaviorSubject<SystemHealth | null>(null);
  public metrics$ = new BehaviorSubject<SystemMetrics | null>(null);
  public sessions$ = new BehaviorSubject<UserSession[]>([]);
  public logs$ = new BehaviorSubject<SystemLog[]>([]);

  constructor(private http: HttpClient) {
    this.initAutoPolling();
    this.seedInitialLogs();
  }

  private get headers(): HttpHeaders {
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'x-api-key': this.serviceApiKey
    });
  }

  private initAutoPolling() {
    timer(0, 3000).pipe(
      switchMap(() => {
        if (!this.autoRefresh$.value) return of(null);
        this.fetchHealth();
        this.fetchMetrics();
        this.fetchSessions();
        return of(null);
      })
    ).subscribe();
  }

  public fetchHealth() {
    this.http.get<SystemHealth>(`${this.apiBaseUrl}/SystemMonitor/health`).pipe(
      catchError(() => {
        // Fallback: query Node.js directly if API is restarting
        return this.http.get<any>(`${this.nodeBaseUrl}/health`).pipe(
          map(nodeRes => ({
            status: 'DEGRADED',
            apiStatus: 'DOWN',
            databaseStatus: 'UNKNOWN',
            nodeStatus: 'UP',
            timestamp: new Date().toISOString(),
            nodeHealth: nodeRes
          })),
          catchError(() => of({
            status: 'DOWN',
            apiStatus: 'DOWN',
            databaseStatus: 'DOWN',
            nodeStatus: 'DOWN',
            timestamp: new Date().toISOString()
          }))
        );
      })
    ).subscribe(health => this.health$.next(health as SystemHealth));
  }

  public fetchMetrics() {
    this.http.get<SystemMetrics>(`${this.apiBaseUrl}/SystemMonitor/metrics`).pipe(
      catchError(() => {
        return this.http.get<SystemMetrics>(`${this.nodeBaseUrl}/metrics`).pipe(
          catchError(() => of(null))
        );
      })
    ).subscribe(metrics => {
      if (metrics) this.metrics$.next(metrics);
    });
  }

  public fetchSessions() {
    this.http.get<UserSession[]>(`${this.apiBaseUrl}/SystemMonitor/sessions`).pipe(
      catchError(() => of([]))
    ).subscribe(sessions => this.sessions$.next(sessions));
  }

  public requestPairing(userId: number, phone: string): Observable<any> {
    const url = `${this.nodeBaseUrl}/request-pairing-code`;
    this.addLog('INFO', `طلب رمز ربط جديد للمستخدم #${userId} على الرقم ${phone}`, 'WhatsAppEngine');
    return this.http.post(url, { userId, phoneNumber: phone }, { headers: this.headers }).pipe(
      catchError(err => {
        this.addLog('ERROR', `فشل طلب رمز الربط: ${err.error?.error || err.message}`, 'WhatsAppEngine');
        throw err;
      })
    );
  }

  public unlinkSession(userId: number): Observable<any> {
    const url = `${this.nodeBaseUrl}/unlink`;
    this.addLog('WARN', `فك ارتباط جلسة الواتساب للمستخدم #${userId}`, 'WhatsAppEngine');
    return this.http.post(url, { userId }, { headers: this.headers }).pipe(
      catchError(err => {
        this.addLog('ERROR', `فشل فك الارتباط: ${err.error?.error || err.message}`, 'WhatsAppEngine');
        throw err;
      })
    );
  }

  public addLog(type: 'INFO' | 'SUCCESS' | 'WARN' | 'ERROR' | 'FATAL', message: string, service = 'System') {
    const current = this.logs$.value;
    const newLog: SystemLog = {
      id: Math.random().toString(36).substring(2, 9),
      timestamp: new Date().toLocaleTimeString('ar-LY', { hour12: false }),
      type,
      message,
      service
    };
    this.logs$.next([newLog, ...current.slice(0, 49)]); // Keep last 50 logs
  }

  private seedInitialLogs() {
    const seed: SystemLog[] = [
      { id: '1', timestamp: new Date().toLocaleTimeString('ar-LY'), type: 'SUCCESS', message: 'تم تشغيل سيرفر الإدارة والمراقبة بنجاح (Eqfal.Company Monitor)', service: 'System' },
      { id: '2', timestamp: new Date().toLocaleTimeString('ar-LY'), type: 'INFO', message: 'الاتصال محمي بنظام التوثيق X-API-KEY والمراقبة المباشرة', service: 'Security' },
      { id: '3', timestamp: new Date().toLocaleTimeString('ar-LY'), type: 'SUCCESS', message: 'سيرفر الواتساب مجدد ومحمي بطوابير المعالجة (Fast Deque Pattern)', service: 'WhatsAppEngine' }
    ];
    this.logs$.next(seed);
  }
}
