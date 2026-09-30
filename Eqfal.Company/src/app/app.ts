import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { MonitorService } from './services/monitor.service';
import { SystemHealth, SystemMetrics, UserSession, SystemLog } from './models/monitor.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit, OnDestroy {
  public health: SystemHealth | null = null;
  public metrics: SystemMetrics | null = null;
  public sessions: UserSession[] = [];
  public logs: SystemLog[] = [];
  public activeTab: 'dashboard' | 'sessions' | 'logs' = 'dashboard';
  public logFilter: string = 'ALL';
  public autoRefresh: boolean = true;
  
  // Pairing Modal state
  public showPairModal: boolean = false;
  public pairUserId: number = 6;
  public pairPhone: string = '';
  public generatedCode: string = '';
  public pairLoading: boolean = false;
  public pairError: string = '';

  private subs: Subscription[] = [];

  constructor(public monitorService: MonitorService) {}

  ngOnInit() {
    this.subs.push(
      this.monitorService.health$.subscribe(h => this.health = h),
      this.monitorService.metrics$.subscribe(m => this.metrics = m),
      this.monitorService.sessions$.subscribe(s => this.sessions = s),
      this.monitorService.logs$.subscribe(l => this.logs = l)
    );
  }

  ngOnDestroy() {
    this.subs.forEach(s => s.unsubscribe());
  }

  public toggleAutoRefresh() {
    this.autoRefresh = !this.autoRefresh;
    this.monitorService.autoRefresh$.next(this.autoRefresh);
  }

  public manualRefresh() {
    this.monitorService.fetchHealth();
    this.monitorService.fetchMetrics();
    this.monitorService.fetchSessions();
    this.monitorService.addLog('INFO', 'تم تحديث بيانات اللوحة يدوياً', 'Dashboard');
  }

  public formatBytes(bytes: number): string {
    if (!bytes || bytes === 0) return '0 MB';
    const mb = bytes / (1024 * 1024);
    return `${mb.toFixed(1)} MB`;
  }

  public formatUptime(seconds?: number): string {
    if (!seconds) return '0 ثانية';
    const hrs = Math.floor(seconds / 3600);
    const mins = Math.floor((seconds % 3600) / 60);
    const secs = Math.floor(seconds % 60);
    if (hrs > 0) return `${hrs} ساعة و ${mins} دقيقة`;
    if (mins > 0) return `${mins} دقيقة و ${secs} ثانية`;
    return `${secs} ثانية`;
  }

  public get filteredLogs(): SystemLog[] {
    if (this.logFilter === 'ALL') return this.logs;
    return this.logs.filter(l => l.type === this.logFilter);
  }

  public openPairModal() {
    this.showPairModal = true;
    this.pairError = '';
    this.generatedCode = '';
  }

  public closePairModal() {
    this.showPairModal = false;
  }

  public submitPairing() {
    if (!this.pairPhone || this.pairPhone.length < 8) {
      this.pairError = 'يرجى إدخال رقم هاتف صحيح';
      return;
    }
    this.pairLoading = true;
    this.pairError = '';
    this.generatedCode = '';

    this.monitorService.requestPairing(this.pairUserId, this.pairPhone).subscribe({
      next: (res) => {
        this.pairLoading = false;
        if (res?.code) {
          this.generatedCode = res.code;
          this.monitorService.addLog('SUCCESS', `تم توليد رمز الربط بنجاح: ${res.code}`, 'WhatsAppEngine');
        } else {
          this.pairError = 'لم يتم إرجاع رمز التوليد';
        }
      },
      error: (err) => {
        this.pairLoading = false;
        this.pairError = err.error?.error || 'فشل توليد رمز الاقتران';
      }
    });
  }

  public triggerUnlink(userId: number) {
    if (!confirm(`هل أنت تأكد من فك ارتباط الحساب #${userId}؟`)) return;
    this.monitorService.unlinkSession(userId).subscribe({
      next: () => {
        this.monitorService.addLog('SUCCESS', `تم فك ارتباط الحساب #${userId} بنجاح`, 'WhatsAppEngine');
        this.manualRefresh();
      },
      error: (err) => {
        alert(err.error?.error || 'فشل فك الارتباط');
      }
    });
  }
}
