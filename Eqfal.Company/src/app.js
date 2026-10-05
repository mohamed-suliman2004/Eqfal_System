const API_BASE = 'https://eqfall.mostanad.ly/api';

// Auth Token & Session Management
let authToken = localStorage.getItem('eqfal_company_token') || '';

function isUserLoggedIn() {
  return sessionStorage.getItem('eqfal_admin_auth') === 'true';
}

function getAuthHeaders() {
  const headers = {
    'Content-Type': 'application/json'
  };
  if (authToken && authToken.trim().length > 0) {
    headers['Authorization'] = `Bearer ${authToken.trim()}`;
  }
  return headers;
}

function checkAuthOnLoad() {
  const loginScreen = document.getElementById('login-screen');
  const appLayout = document.getElementById('app-layout');
  if (isUserLoggedIn()) {
    if (loginScreen) {
      loginScreen.classList.add('hidden');
      loginScreen.style.display = 'none';
    }
    if (appLayout) {
      appLayout.classList.remove('hidden');
      appLayout.style.display = 'flex';
    }
    fetchData();
    fetchSubscriptionStats();
  } else {
    if (loginScreen) {
      loginScreen.classList.remove('hidden');
      loginScreen.style.display = 'flex';
    }
    if (appLayout) {
      appLayout.classList.add('hidden');
      appLayout.style.display = 'none';
    }
  }
}

function showLoginModal() {
  doLogout();
}

function hideLoginModal() {
  const loginScreen = document.getElementById('login-screen');
  if (loginScreen) {
    loginScreen.classList.add('hidden');
    loginScreen.style.display = 'none';
  }
}

function togglePasswordVisibility() {
  const pwdInput = document.getElementById('login-password');
  const eyeIcon = document.getElementById('pwd-eye-icon');
  if (!pwdInput) return;
  if (pwdInput.type === 'password') {
    pwdInput.type = 'text';
    if (eyeIcon) eyeIcon.textContent = 'visibility_off';
  } else {
    pwdInput.type = 'password';
    if (eyeIcon) eyeIcon.textContent = 'visibility';
  }
}

function fillAdminCredentials() {
  const usernameInput = document.getElementById('login-username');
  const passwordInput = document.getElementById('login-password');
  if (usernameInput) usernameInput.value = 'eqfall';
  if (passwordInput) passwordInput.value = 'eqfall$';
  const errDiv = document.getElementById('login-error');
  if (errDiv) errDiv.classList.add('hidden');
}

async function doLogin(e) {
  if (e && e.preventDefault) e.preventDefault();
  const usernameInput = document.getElementById('login-username');
  const passwordInput = document.getElementById('login-password');
  const username = usernameInput ? usernameInput.value.trim() : '';
  const password = passwordInput ? passwordInput.value.trim() : '';
  const errDiv = document.getElementById('login-error');
  if (errDiv) errDiv.classList.add('hidden');

  if (!username || !password) {
    if (errDiv) {
      errDiv.innerText = 'يرجى إدخال اسم المستخدم وكلمة المرور';
      errDiv.classList.remove('hidden');
    }
    return false;
  }

  // Hardcoded Admin Access (eqfall / eqfall$) as requested
  if (username.toLowerCase() === 'eqfall' && password === 'eqfall$') {
    sessionStorage.setItem('eqfal_admin_auth', 'true');
    sessionStorage.setItem('eqfal_admin_user', 'مسؤول النظام');
    authToken = 'EQFAL_ADMIN_AUTHORIZED_SESSION';
    localStorage.setItem('eqfal_company_token', authToken);

    const loginScreen = document.getElementById('login-screen');
    const appLayout = document.getElementById('app-layout');
    if (loginScreen) {
      loginScreen.classList.add('hidden');
      loginScreen.style.display = 'none';
    }
    if (appLayout) {
      appLayout.classList.remove('hidden');
      appLayout.style.display = 'flex';
    }

    showToast('أهلاً بك مجدداً!', 'تم تسجيل الدخول بنجاح إلى لوحة المراقبة والعمليات', 'success');
    addLog('SUCCESS', 'تسجيل دخول ناجح للوحة العمليات: eqfall', 'Auth');
    fetchData();
    fetchSubscriptionStats();
    return false;
  }

  // Fallback to API if other credentials entered
  try {
    const res = await fetch(`${API_BASE}/Auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    });
    const data = await res.json();
    if (res.ok && data.token) {
      authToken = data.token;
      localStorage.setItem('eqfal_company_token', authToken);
      sessionStorage.setItem('eqfal_admin_auth', 'true');
      sessionStorage.setItem('eqfal_admin_user', data.user?.name || username);

      const loginScreen = document.getElementById('login-screen');
      const appLayout = document.getElementById('app-layout');
      if (loginScreen) {
        loginScreen.classList.add('hidden');
        loginScreen.style.display = 'none';
      }
      if (appLayout) {
        appLayout.classList.remove('hidden');
        appLayout.style.display = 'flex';
      }

      showToast('مرحباً بك مجدداً!', `تم تسجيل الدخول بصلاحيات ${data.user?.name || username}`, 'success');
      addLog('SUCCESS', `تم تسجيل الدخول بنجاح: ${data.user?.name || username}`, 'Auth');
      fetchData();
      fetchSubscriptionStats();
    } else {
      if (errDiv) {
        errDiv.innerText = data.message || 'بيانات الدخول غير صحيحة. يرجى التأكد من اسم المستخدم وكلمة المرور.';
        errDiv.classList.remove('hidden');
      }
    }
  } catch (err) {
    if (errDiv) {
      errDiv.innerText = 'خطأ في الاتصال بالخادم أو بيانات الدخول غير مطابقة.';
      errDiv.classList.remove('hidden');
    }
  }
  return false;
}

function doLogout() {
  authToken = '';
  localStorage.removeItem('eqfal_company_token');
  sessionStorage.removeItem('eqfal_admin_auth');
  sessionStorage.removeItem('eqfal_admin_user');

  const appLayout = document.getElementById('app-layout');
  const loginScreen = document.getElementById('login-screen');
  if (appLayout) {
    appLayout.classList.add('hidden');
    appLayout.style.display = 'none';
  }
  if (loginScreen) {
    loginScreen.classList.remove('hidden');
    loginScreen.style.display = 'flex';
  }

  addLog('INFO', 'تم تسجيل الخروج من لوحة العمليات', 'Auth');
}

// Sidebar Controls
function toggleSidebar() {
  const sidebar = document.getElementById('sidebar');
  const toggleIcon = document.getElementById('sidebar-toggle-icon');
  if (!sidebar) return;
  sidebar.classList.toggle('collapsed');
  const isCollapsed = sidebar.classList.contains('collapsed');
  if (toggleIcon) {
    toggleIcon.textContent = isCollapsed ? 'menu' : 'menu_open';
  }
  localStorage.setItem('eqfal_sidebar_collapsed', isCollapsed ? 'true' : 'false');
}

function toggleMobileSidebar() {
  const sidebar = document.getElementById('sidebar');
  const backdrop = document.getElementById('sidebar-backdrop');
  if (!sidebar) return;
  sidebar.classList.toggle('mobile-open');
  if (backdrop) {
    if (sidebar.classList.contains('mobile-open')) {
      backdrop.classList.remove('hidden');
      setTimeout(() => backdrop.classList.remove('opacity-0'), 10);
    } else {
      backdrop.classList.add('opacity-0');
      setTimeout(() => backdrop.classList.add('hidden'), 300);
    }
  }
}

// Theme Controls (Inside Dashboard: Light Mode by Default)
function getSavedTheme() {
  return localStorage.getItem('eqfal_company_theme') || 'light';
}

function applyTheme(theme) {
  document.documentElement.setAttribute('data-theme', theme);
  const isDark = theme === 'dark';
  if (isDark) {
    document.documentElement.classList.add('dark');
  } else {
    document.documentElement.classList.remove('dark');
  }
  localStorage.setItem('eqfal_company_theme', theme);

  document.querySelectorAll('.theme-icon-indicator').forEach(el => {
    el.textContent = isDark ? 'light_mode' : 'dark_mode';
  });
  document.querySelectorAll('.theme-label-indicator').forEach(el => {
    el.textContent = isDark ? 'الوضع النهاري' : 'الوضع الليلي';
  });
}

function toggleTheme() {
  const current = getSavedTheme();
  const next = current === 'dark' ? 'light' : 'dark';
  applyTheme(next);
}

// Toast Notifications Helper
function showToast(title, message, type = 'success') {
  let toastContainer = document.getElementById('toast-container');
  if (!toastContainer) {
    toastContainer = document.createElement('div');
    toastContainer.id = 'toast-container';
    toastContainer.className = 'fixed top-5 left-1/2 -translate-x-1/2 z-[200] space-y-3 w-full max-w-md px-4 pointer-events-none';
    document.body.appendChild(toastContainer);
  }

  const toast = document.createElement('div');
  const borderBg = type === 'success'
    ? 'border-emerald-500/50 bg-slate-900/95 text-emerald-200 shadow-emerald-950/50'
    : 'border-indigo-500/50 bg-slate-900/95 text-white shadow-indigo-950/50';
  const iconBg = type === 'success' ? 'bg-emerald-500/20 text-emerald-400' : 'bg-indigo-500/20 text-indigo-400';
  const iconName = type === 'success' ? 'check_circle' : 'info';

  toast.className = `glass-card p-4 border ${borderBg} shadow-2xl rounded-2xl flex items-start gap-3 pointer-events-auto transition-all duration-500 transform translate-y-0 opacity-100`;
  toast.innerHTML = `
    <div class="w-9 h-9 rounded-xl ${iconBg} flex items-center justify-center shrink-0">
      <span class="material-symbols-outlined text-xl">${iconName}</span>
    </div>
    <div class="flex-1">
      <h4 class="font-bold text-sm text-white flex items-center gap-1.5">${title}</h4>
      <p class="text-xs text-slate-300 mt-1 leading-relaxed">${message}</p>
    </div>
    <button onclick="this.parentElement.remove()" class="text-slate-400 hover:text-white p-1 transition">
      <span class="material-symbols-outlined text-sm">close</span>
    </button>
  `;
  toastContainer.appendChild(toast);

  setTimeout(() => {
    toast.classList.add('opacity-0', '-translate-y-3');
    setTimeout(() => toast.remove(), 500);
  }, 6000);
}

let logs = [];
let currentTab = 'dashboard';
let prevSessionStates = {};
let currentOtpUserId = null;
let notifiedConnectedUsers = new Set();

function switchTab(tab) {
  currentTab = tab;

  // Update sidebar navigation buttons
  document.querySelectorAll('.sidebar-nav-btn').forEach(btn => {
    btn.classList.remove('active');
  });
  const activeBtn = document.getElementById(`tab-${tab}`);
  if (activeBtn) {
    activeBtn.classList.add('active');
    const title = activeBtn.getAttribute('data-title') || activeBtn.innerText.trim();
    const titleElem = document.getElementById('current-tab-title');
    if (titleElem) titleElem.textContent = title;
  }

  // Also update any legacy .tab-btn classes
  document.querySelectorAll('.tab-btn').forEach(btn => {
    btn.classList.remove('bg-indigo-600', 'text-white');
    btn.classList.add('text-slate-300');
  });

  document.getElementById('content-dashboard')?.classList.toggle('hidden', tab !== 'dashboard');
  document.getElementById('content-subscriptions')?.classList.toggle('hidden', tab !== 'subscriptions');
  document.getElementById('content-pricing-plans')?.classList.toggle('hidden', tab !== 'pricing-plans');
  document.getElementById('content-payments')?.classList.toggle('hidden', tab !== 'payments');
  document.getElementById('content-marketers')?.classList.toggle('hidden', tab !== 'marketers');
  document.getElementById('content-sessions')?.classList.toggle('hidden', tab !== 'sessions');
  document.getElementById('content-activity')?.classList.toggle('hidden', tab !== 'activity');
  document.getElementById('content-logs')?.classList.toggle('hidden', tab !== 'logs');

  // Close mobile sidebar drawer if open
  if (window.innerWidth < 1024) {
    const sidebar = document.getElementById('sidebar');
    if (sidebar && sidebar.classList.contains('mobile-open')) {
      toggleMobileSidebar();
    }
  }

  if (tab === 'subscriptions') {
    fetchSubscriptions();
    fetchSubscriptionStats();
  } else if (tab === 'pricing-plans') {
    fetchPricingPlans();
    fetchSubscriptionSettings();
  } else if (tab === 'payments') {
    fetchPayments();
    fetchPaymentsSummary();
  } else if (tab === 'marketers') {
    fetchMarketers();
    fetchDiscountCodes();
  } else if (tab === 'logs') {
    fetchAuditLogs();
  } else if (tab === 'activity') {
    fetchUserActivity();
  } else if (tab === 'sessions') {
    fetchOtpSetting();
  }
}

function addLog(type, message, service = 'System') {
  const time = new Date().toLocaleTimeString('ar-LY', { hour12: false });
  logs.unshift({ time, type, message, service });
  if (logs.length > 50) logs.pop();
  renderLogs();
}

function renderLogs() {
  const container = document.getElementById('logs-container');
  if (!container) return;
  if (logs.length === 0) {
    container.innerHTML = `<div class="text-slate-600 text-center py-6">لا توجد أحداث مسجلة حالياً</div>`;
    return;
  }
  container.innerHTML = logs.map(l => {
    let typeClass = 'text-indigo-400';
    if (l.type === 'SUCCESS') typeClass = 'text-emerald-400 font-bold';
    if (l.type === 'ERROR') typeClass = 'text-rose-400 font-bold';
    if (l.type === 'WARN') typeClass = 'text-amber-400 font-bold';
    return `
      <div class="flex items-start gap-3 p-2 rounded hover:bg-slate-900/50 transition">
        <span class="text-slate-500 shrink-0">${l.time}</span>
        <span class="${typeClass}">[${l.type}]</span>
        <span class="text-slate-400 shrink-0">[${l.service}]:</span>
        <span class="text-slate-200 flex-1">${l.message}</span>
      </div>
    `;
  }).join('');
}

function clearLogs() {
  logs = [];
  renderLogs();
}

async function authFetch(url, options = {}) {
  const res = await fetch(url, {
    ...options,
    headers: { ...getAuthHeaders(), ...(options.headers || {}) }
  });
  return res;
}

// Format utilities
function formatBytes(bytes) {
  if (!bytes || bytes === 0) return '0 MB';
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
}

function formatUptime(sec) {
  if (!sec) return '0 دقيقة';
  const mins = Math.floor(sec / 60);
  const hours = Math.floor(mins / 60);
  if (hours > 0) return `${hours} ساعة و ${mins % 60} دقيقة`;
  return `${mins} دقيقة`;
}

let currentAuditType = 'all';
let currentAuditSearch = '';
let cachedAuditLogs = [];
let auditSearchTimer = null;
let cachedSessionsList = [];

async function fetchOtpSetting(sessions = null) {
  if (sessions && Array.isArray(sessions) && sessions.length > 0) {
    cachedSessionsList = sessions;
  }
  const effectiveSessions = (sessions && Array.isArray(sessions) && sessions.length > 0) 
    ? sessions 
    : cachedSessionsList;

  try {
    const res = await authFetch(`${API_BASE}/SystemMonitor/otp-setting`);
    if (res.ok) {
      const data = await res.json();
      currentOtpUserId = data.userId;

      const isConnected = data.isConnected || data.status === 'متصل';

      // 1. Sync Critical Alert Banner
      const alertBanner = document.getElementById('otp-critical-alert');
      if (alertBanner) {
        if (!isConnected) {
          alertBanner.classList.remove('hidden');
        } else {
          alertBanner.classList.add('hidden');
        }
      }

      // 2. Sync Badge & Status Displays
      const badge = document.getElementById('otp-sender-badge');
      if (badge) {
        badge.innerHTML = `<span class="material-symbols-outlined text-xs">${isConnected ? 'verified' : 'warning'}</span> المعتمد حالياً: #${data.userId} (${data.fullName || 'مستخدم'}) [${data.status || (isConnected ? 'متصل' : 'غير متصل')}]`;
        badge.className = isConnected ? 'badge-up text-xs font-semibold' : 'badge-down text-xs font-semibold';
      }

      const display = document.getElementById('current-otp-user-display');
      if (display) {
        const stClass = isConnected ? 'text-emerald-400' : 'text-rose-400';
        display.innerHTML = `الرقم المعتمد: <strong>#${data.userId} (${data.fullName || 'مستخدم'} - ${data.phone || 'بدون رقم'})</strong> <span class="${stClass}">[${data.status || (isConnected ? 'متصل' : 'غير متصل')}]</span>`;
      }

      // 3. Populate Select (supports both otp-sender-select and otp-user-select)
      const select = document.getElementById('otp-sender-select') || document.getElementById('otp-user-select');
      if (select && effectiveSessions && effectiveSessions.length > 0) {
        // Sort sessions: connected devices first, then by ID
        const sorted = [...effectiveSessions].sort((a, b) => {
          const aConn = (a.status === 'متصل' || a.isConnected) ? 1 : 0;
          const bConn = (b.status === 'متصل' || b.isConnected) ? 1 : 0;
          if (bConn !== aConn) return bConn - aConn;
          return a.userId - b.userId;
        });

        select.innerHTML = sorted.map(s => {
          const conn = s.status === 'متصل' || s.isConnected;
          const icon = conn ? '🟢' : '⚪';
          const st = conn ? 'متصل' : 'غير متصل';
          return `
            <option value="${s.userId}" ${s.userId === currentOtpUserId ? 'selected' : ''}>
              ${icon} #${s.userId} - ${s.fullName || 'مستخدم'} (${s.phone || 'بدون رقم'}) [${st}]
            </option>
          `;
        }).join('');
        select.dataset.loaded = 'true';
      }
    }
  } catch (e) {
    console.error('fetchOtpSetting error:', e);
  }
}

async function saveOtpSender() {
  const select = document.getElementById('otp-sender-select') || document.getElementById('otp-user-select');
  const userId = select ? select.value : null;

  if (!userId) {
    alert('يرجى اختيار رقم المستخدم المعتمد أولاً');
    return;
  }

  const btn = document.getElementById('save-otp-btn');
  if (btn) {
    btn.disabled = true;
    btn.innerHTML = '<span class="material-symbols-outlined text-base animate-spin">sync</span> جاري الحفظ...';
  }

  try {
    const res = await authFetch(`${API_BASE}/SystemMonitor/otp-setting`, {
      method: 'POST',
      body: JSON.stringify({ userId: parseInt(userId) })
    });
    const data = await res.json();

    if (res.ok && (data.success || data.userId)) {
      currentOtpUserId = parseInt(userId);
      showToast('تم حفظ الرقم بنجاح!', data.message || `تم تعيين المستخدم #${userId} كالرقم الرسمي للـ OTP`, 'success');
      addLog('SUCCESS', data.message || `تعيين المستخدم #${userId} كالرقم الرسمي للـ OTP`, 'SystemConfig');
      await fetchData();
    } else {
      alert(data.message || data.error || 'حدث خطأ أثناء حفظ الإعدادات');
    }
  } catch (e) {
    alert('حدث خطأ في الاتصال بالخادم أثناء حفظ الإعداد');
  } finally {
    if (btn) {
      btn.disabled = false;
      btn.innerHTML = '<span class="material-symbols-outlined text-base">save</span> حفظ الرقم الرسمي';
    }
  }
}
window.saveOtpSetting = saveOtpSender;

function showApiOfflineBanner(status) {
  const banner = document.getElementById('api-offline-banner');
  const title = document.getElementById('api-offline-title');
  if (banner) {
    banner.classList.remove('hidden');
    if (title) {
      title.innerText = `تنبيه: تعذر الاتصال بخادم الـ API الرئيسي (${status || '503 Service Unavailable'})`;
    }
  }
}

function hideApiOfflineBanner() {
  const banner = document.getElementById('api-offline-banner');
  if (banner) {
    banner.classList.add('hidden');
  }
}

async function fetchData() {

  // 1. Fetch Health
  try {
    const res = await authFetch(`${API_BASE}/SystemMonitor/health`);
    if (res.ok) {
      const data = await res.json();
      hideApiOfflineBanner();
      document.getElementById('api-status-badge').innerHTML = `<span class="badge-up"><span class="material-symbols-outlined text-sm">check_circle</span> يعمل بكفاءة</span>`;
      document.getElementById('node-status-badge').innerHTML = `<span class="badge-up"><span class="material-symbols-outlined text-sm">check_circle</span> متصل ونشط</span>`;
      document.getElementById('db-status-badge').innerHTML = `<span class="badge-up"><span class="material-symbols-outlined text-sm">check_circle</span> متصلة وسليمة</span>`;

      if (data.nodeHealth) {
        document.getElementById('metric-uptime').innerText = formatUptime(data.nodeHealth.uptimeSeconds);
        document.getElementById('metric-ram-used').innerText = formatBytes(data.nodeHealth.memoryUsage?.heapUsed);
        document.getElementById('metric-ram-total').innerText = 'الإجمالي: ' + formatBytes(data.nodeHealth.memoryUsage?.rss);
      }
    } else {
      showApiOfflineBanner(res.status);
      document.getElementById('api-status-badge').innerHTML = `<span class="badge-down"><span class="material-symbols-outlined text-sm">cancel</span> غير متصل (${res.status})</span>`;
      document.getElementById('node-status-badge').innerHTML = `<span class="badge-down"><span class="material-symbols-outlined text-sm">cancel</span> غير متاح</span>`;
      document.getElementById('db-status-badge').innerHTML = `<span class="badge-down"><span class="material-symbols-outlined text-sm">cancel</span> غير متاح</span>`;
    }
  } catch (e) {
    if (e.message === 'Unauthorized') return;
    showApiOfflineBanner('Network Error');
    document.getElementById('api-status-badge').innerHTML = `<span class="badge-down"><span class="material-symbols-outlined text-sm">cancel</span> خطأ اتصال</span>`;
    document.getElementById('node-status-badge').innerHTML = `<span class="badge-down"><span class="material-symbols-outlined text-sm">cancel</span> غير متاح</span>`;
    document.getElementById('db-status-badge').innerHTML = `<span class="badge-down"><span class="material-symbols-outlined text-sm">cancel</span> غير متاح</span>`;
  }

  // 2. Fetch Metrics
  try {
    const res = await authFetch(`${API_BASE}/SystemMonitor/metrics`);
    if (res.ok) {
      const m = await res.json();
      document.getElementById('metric-active-sessions').innerText = (m.activeSessions ?? m.activeSessionsCount ?? 1);
      document.getElementById('metric-msg-received').innerText = m.messagesReceived ?? 0;
      document.getElementById('metric-msg-sent').innerText = m.messagesSent ?? 0;
      document.getElementById('metric-wh-success').innerText = m.webhookSuccess ?? 0;
      document.getElementById('metric-wh-failures').innerText = m.webhookFailures ?? 0;
      document.getElementById('metric-wh-dropped').innerText = m.webhookDropped ?? 0;
      document.getElementById('metric-queue-length').innerText = (m.webhookQueueLength ?? 0) + ' رسالة';
      document.getElementById('metric-workers').innerText = (m.activeWebhookWorkers ?? 0) + ' / 5';
      document.getElementById('metric-pairings').innerText = (m.activePairingsCount ?? 0) + ' / 5';
      document.getElementById('metric-auth-stores').innerText = (m.authStoresCount ?? 1) + ' مستخدم';
    }
  } catch (e) {}

  // 3. Fetch Sessions & Monitor State Changes
  try {
    const res = await authFetch(`${API_BASE}/SystemMonitor/sessions`);
    if (res.ok) {
      const data = await res.json();
      const sessions = Array.isArray(data) ? data : (data.value || []);
      document.getElementById('sessions-badge').innerText = sessions.length;
      
      // Fetch and sync OTP sender setting dropdown with active sessions
      fetchOtpSetting(sessions);

      // Detect newly connected devices
      const pairModal = document.getElementById('pair-modal');
      const isModalOpen = pairModal && !pairModal.classList.contains('hidden');

      sessions.forEach(s => {
        const oldState = prevSessionStates[s.userId];
        if (isModalOpen && oldState === 'جاري الاتصال' && s.status === 'متصل') {
          showToast('تم ربط الجهاز بنجاح!', `تم الاتصال بحساب الواتساب للمستخدم ${s.fullName} (#${s.userId}) بنجاح!`, 'success');
          addLog('SUCCESS', `تم ربط حساب المستخدم ${s.fullName} (#${s.userId}) بنجاح!`, 'WhatsAppEngine');
          closePairModal();
        }

        // Detect device disconnections
        if (oldState === 'متصل' && (s.status === 'غير متصل' || s.status === 'جاري الاتصال')) {
          addLog('WARN', `انقطع اتصال حساب المستخدم ${s.fullName} (#${s.userId})!`, 'WhatsAppEngine');
        }

        prevSessionStates[s.userId] = s.status;
      });

      const tbody = document.getElementById('sessions-table-body');
      if (sessions.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" class="p-8 text-center text-slate-500 text-sm font-semibold">لا توجد حسابات أو أجهزة مسجلة</td></tr>`;
      } else {
        tbody.innerHTML = sessions.map(s => {
          let badgeHtml = '';
          if (s.status === 'متصل') {
            badgeHtml = `<span class="badge-up"><span class="material-symbols-outlined text-sm">check_circle</span> متصل</span>`;
          } else if (s.status === 'جاري الاتصال') {
            badgeHtml = `<span class="bg-amber-500/15 text-amber-600 dark:text-amber-300 border border-amber-500/30 px-3 py-1 rounded-full text-xs font-semibold inline-flex items-center gap-1.5"><span class="material-symbols-outlined text-sm animate-spin">sync</span> جاري الاتصال...</span>`;
          } else {
            badgeHtml = `<span class="badge-down"><span class="material-symbols-outlined text-sm">radio_button_unchecked</span> ${s.status || 'غير متصل'}</span>`;
          }

          let actionBtn = '';
          if (s.status === 'متصل') {
            actionBtn = `
              <button onclick="triggerUnlink(${s.userId})" class="px-3 py-1.5 rounded-lg bg-rose-500/10 text-rose-500 dark:text-rose-400 border border-rose-500/20 hover:bg-rose-500/20 text-xs font-medium transition inline-flex items-center gap-1">
                <span class="material-symbols-outlined text-xs">link_off</span> إلغاء الربط
              </button>`;
          } else {
            actionBtn = `
              <button onclick="openPairModal(${s.userId}, '${s.phone || ''}')" class="px-3 py-1.5 rounded-lg bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20 hover:bg-emerald-500/20 text-xs font-medium transition inline-flex items-center gap-1">
                <span class="material-symbols-outlined text-xs">add_link</span> ربط الجهاز
              </button>`;
          }

          const userFullName = (s.fullName && s.fullName.trim()) ? s.fullName.trim() : ('مستخدم #' + s.userId);
          const userAccount = s.username || s.email || '—';

          return `
            <tr class="hover:bg-slate-50 dark:hover:bg-slate-800/40 transition">
              <td class="p-4 font-mono font-bold text-slate-500">#${s.userId}</td>
              <td class="p-4 font-bold text-slate-900 dark:text-white flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-indigo-500/15 text-indigo-600 dark:text-indigo-400 border border-indigo-500/30 flex items-center justify-center font-bold text-xs shrink-0">
                  ${(userFullName || 'م').charAt(0)}
                </div>
                <span>${userFullName}</span>
                ${s.userId === currentOtpUserId ? '<span class="bg-amber-500/20 text-amber-700 dark:text-amber-300 border border-amber-500/40 text-[10px] px-2 py-0.5 rounded-full font-bold">رسمي OTP</span>' : ''}
              </td>
              <td class="p-4 font-mono text-indigo-600 dark:text-indigo-400 font-semibold">${userAccount}</td>
              <td class="p-4 text-slate-700 dark:text-slate-300 font-mono">${s.phone || 'غير مسجل'}</td>
              <td class="p-4">${badgeHtml}</td>
              <td class="p-4 text-slate-500 dark:text-slate-400 text-xs">${s.lastConnected ? new Date(s.lastConnected).toLocaleString('ar-LY') : 'لم يتصل بعد'}</td>
              <td class="p-4 text-center">
                ${actionBtn}
              </td>
            </tr>
          `;
        }).join('');
      }
    } else {
      const tbody = document.getElementById('sessions-table-body');
      if (tbody) tbody.innerHTML = `<tr><td colspan="7" class="p-8 text-center text-amber-500 font-medium">⚠️ تعذر تحميل جلسات الواتساب (${res.status}). يرجى التحقق من تشغيل خادم الـ API.</td></tr>`;
    }
  } catch (e) {
    const tbody = document.getElementById('sessions-table-body');
    if (tbody) tbody.innerHTML = `<tr><td colspan="7" class="p-8 text-center text-rose-500 font-medium">⚠️ تعذر الاتصال بالخادم لجلب الجلسات.</td></tr>`;
  }

  // 4. Fetch User Activity & Audit Logs
  fetchUserActivity();
  fetchAuditLogs();
}

async function fetchUserActivity() {
  try {
    const res = await authFetch(`${API_BASE}/SystemMonitor/user-activity`);
    if (res.ok) {
      const json = await res.json();
      const list = Array.isArray(json) ? json : (json.data || []);
      const tbody = document.getElementById('user-activity-table-body');
      if (!tbody) return;

      if (list.length === 0) {
        tbody.innerHTML = `<tr><td colspan="8" class="p-8 text-center text-slate-500 font-semibold">لا توجد بيانات نشاط للمستخدمين</td></tr>`;
      } else {
        tbody.innerHTML = list.map(u => {
          let badgeHtml = u.status === 'متصل'
            ? `<span class="badge-up"><span class="material-symbols-outlined text-sm">check_circle</span> متصل</span>`
            : `<span class="badge-down"><span class="material-symbols-outlined text-sm">radio_button_unchecked</span> ${u.status || 'غير متصل'}</span>`;

          const uFullName = (u.fullName && u.fullName.trim()) ? u.fullName.trim() : ('مستخدم #' + u.userId);
          const uAccount = u.username || u.email || u.userName || '—';

          return `
            <tr class="hover:bg-slate-50 dark:hover:bg-slate-800/40 transition">
              <td class="p-4 font-mono font-bold text-slate-500">#${u.userId}</td>
              <td class="p-4 font-bold text-slate-900 dark:text-white flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-indigo-500/15 text-indigo-600 dark:text-indigo-400 border border-indigo-500/30 flex items-center justify-center font-bold text-xs shrink-0">
                  ${(uFullName || 'م').charAt(0)}
                </div>
                <span>${uFullName}</span>
              </td>
              <td class="p-4 font-mono text-indigo-600 dark:text-indigo-400 font-semibold">${uAccount}</td>
              <td class="p-4 text-slate-700 dark:text-slate-300 font-mono">${u.phone || 'غير مسجل'}</td>
              <td class="p-4 text-center font-bold text-indigo-600 dark:text-indigo-400">${u.operationsProcessed} عملية</td>
              <td class="p-4 text-center font-mono text-emerald-600 dark:text-emerald-400 font-bold">${u.inboundMessages || 0} رسالة</td>
              <td class="p-4 text-center font-mono text-cyan-600 dark:text-cyan-400 font-bold">${u.outboundMessages || 0} رسالة</td>
              <td class="p-4 text-center">${badgeHtml}</td>
            </tr>
          `;
        }).join('');
      }
    } else {
      const tbody = document.getElementById('user-activity-table-body');
      if (tbody) tbody.innerHTML = `<tr><td colspan="8" class="p-8 text-center text-amber-500 font-medium">⚠️ تعذر تحميل نشاط المستخدمين (${res.status}). تأكد من تشغيل الـ API.</td></tr>`;
    }
  } catch (e) {
    const tbody = document.getElementById('user-activity-table-body');
    if (tbody) tbody.innerHTML = `<tr><td colspan="8" class="p-8 text-center text-rose-500 font-medium">⚠️ خطأ اتصال أثناء جلب نشاط المستخدمين.</td></tr>`;
  }
}

async function fetchAuditLogs() {
  try {
    const params = new URLSearchParams();
    params.append('limit', '100');
    if (currentAuditType && currentAuditType !== 'all') {
      params.append('type', currentAuditType);
    }
    if (currentAuditSearch && currentAuditSearch.trim()) {
      params.append('search', currentAuditSearch.trim());
    }

    const res = await authFetch(`${API_BASE}/SystemMonitor/audit-logs?${params.toString()}`);
    if (res.ok) {
      const json = await res.json();
      const logsList = Array.isArray(json) ? json : (json.data || []);
      cachedAuditLogs = logsList;
      const container = document.getElementById('audit-logs-container');
      if (!container) return;

      if (logsList.length === 0) {
        container.innerHTML = `<div class="text-slate-500 text-center py-6 text-xs">لا توجد سجلات مطابقة للبحث أو الفلتر الحالي</div>`;
      } else {
        container.innerHTML = logsList.map(a => {
          const act = a.action || '';
          const det = a.details || '';
          const isError = act.includes('ERROR') || act.includes('FAIL') || det.includes('فشل') || det.includes('خطأ');
          const isSec = act.includes('SECURITY') || act.includes('PASSWORD') || act.includes('OTP') || act.includes('AUTH');
          
          let badgeColor = 'text-indigo-400';
          let icon = 'info';
          if (isError) {
            badgeColor = 'text-rose-400';
            icon = 'error';
          } else if (isSec) {
            badgeColor = 'text-amber-400';
            icon = 'security';
          }

          return `
            <div class="flex items-start gap-3 p-2.5 rounded hover:bg-slate-900/60 transition border-b border-slate-900">
              <span class="text-slate-500 shrink-0 text-xs">${new Date(a.timestamp).toLocaleTimeString('ar-LY')}</span>
              <span class="${badgeColor} font-bold shrink-0 text-xs flex items-center gap-1">
                <span class="material-symbols-outlined text-xs">${icon}</span> [${a.action}]
              </span>
              <span class="text-indigo-300 font-semibold shrink-0 text-xs">${a.userName}:</span>
              <span class="text-slate-200 flex-1 text-xs">${a.details}</span>
              <span class="text-slate-500 text-[10px] shrink-0 font-mono">${a.ipAddress || '127.0.0.1'}</span>
            </div>
          `;
        }).join('');
      }
    } else {
      const container = document.getElementById('audit-logs-container');
      if (container) {
        container.innerHTML = `<div class="text-amber-500 text-center py-6 text-xs font-semibold">⚠️ تعذر تحميل سجل الأحداث من الخادم (${res.status}). يرجى التحقق من تشغيل خادم الـ API.</div>`;
      }
    }
  } catch (e) {
    console.error('fetchAuditLogs error:', e);
    const container = document.getElementById('audit-logs-container');
    if (container) {
      container.innerHTML = `<div class="text-rose-500 text-center py-6 text-xs font-semibold">⚠️ تعذر الاتصال بالخادم لجلب سجل الأحداث.</div>`;
    }
  }
}

function filterAuditType(type) {
  currentAuditType = type;
  const types = ['all', 'error', 'security', 'operations'];
  types.forEach(t => {
    const btn = document.getElementById(`filter-btn-${t}`);
    if (btn) {
      if (t === type) {
        btn.className = 'px-3 py-1 rounded-lg bg-indigo-600 text-white font-medium transition';
      } else {
        btn.className = 'px-3 py-1 rounded-lg bg-slate-800 text-slate-300 hover:text-white transition';
      }
    }
  });
  fetchAuditLogs();
}

function debounceAuditSearch() {
  clearTimeout(auditSearchTimer);
  auditSearchTimer = setTimeout(() => {
    const input = document.getElementById('audit-search-input');
    currentAuditSearch = input ? input.value : '';
    fetchAuditLogs();
  }, 300);
}

async function cleanupAuditLogs(days = 30) {
  if (!confirm(`هل أنت متأكد من حذف السجلات القديمة التي مر عليها أكثر من ${days} يوماً؟ سيساعد هذا في تسريع قاعدة البيانات.`)) return;

  try {
    const res = await authFetch(`${API_BASE}/SystemMonitor/cleanup-audit-logs?days=${days}`, {
      method: 'POST'
    });
    const data = await res.json();
    if (res.ok && data.success) {
      showToast('تم التطهير بنجاح!', data.message, 'success');
      addLog('INFO', data.message, 'AuditCleanup');
      fetchAuditLogs();
    } else {
      alert(data.message || 'حدث خطأ أثناء تطهير السجلات');
    }
  } catch (e) {
    alert('حدث خطأ في الاتصال بالخادم أثناء طلب التطهير');
  }
}

function exportAuditLogsCsv() {
  if (!cachedAuditLogs || cachedAuditLogs.length === 0) {
    alert('لا توجد سجلات حالية للتصدير');
    return;
  }

  let csvContent = '\uFEFF'; // UTF-8 BOM for Excel Arabic support
  csvContent += 'المعرف,التاريخ والوقت,اسم المستخدم,الإجراء,التفاصيل,عنوان IP\r\n';

  cachedAuditLogs.forEach(l => {
    const timeStr = `"${new Date(l.timestamp).toLocaleString('ar-LY')}"`;
    const user = `"${(l.userName || '').replace(/"/g, '""')}"`;
    const action = `"${(l.action || '').replace(/"/g, '""')}"`;
    const details = `"${(l.details || '').replace(/"/g, '""')}"`;
    const ip = `"${(l.ipAddress || '').replace(/"/g, '""')}"`;
    csvContent += `${l.id},${timeStr},${user},${action},${details},${ip}\r\n`;
  });

  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.setAttribute('href', url);
  link.setAttribute('download', `Eqfal_AuditLogs_${new Date().toISOString().slice(0, 10)}.csv`);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  showToast('تم التصدير!', 'تم تنزيل ملف السجلات بنجاح', 'success');
}

function openPairModal(userId = '', phone = '') {
  document.getElementById('pair-modal').classList.remove('hidden');
  document.getElementById('pair-user-id').value = userId;
  document.getElementById('pair-phone').value = phone;
  document.getElementById('pair-error').classList.add('hidden');
  document.getElementById('pair-result').classList.add('hidden');
}

function closePairModal() {
  document.getElementById('pair-modal').classList.add('hidden');
}

async function submitPairing() {
  const userId = document.getElementById('pair-user-id').value;
  const phone = document.getElementById('pair-phone').value;
  const errDiv = document.getElementById('pair-error');
  const resDiv = document.getElementById('pair-result');
  const btn = document.getElementById('pair-submit-btn');

  if (!phone || phone.length < 8) {
    errDiv.innerText = 'يرجى إدخال رقم هاتف صحيح مع مفتاح الدولة';
    errDiv.classList.remove('hidden');
    return;
  }

  errDiv.classList.add('hidden');
  resDiv.classList.add('hidden');
  btn.disabled = true;
  btn.innerText = 'جاري طلب الرمز...';

  try {
    const res = await authFetch(`${API_BASE}/WhatsApp/pair`, {
      method: 'POST',
      body: JSON.stringify({ userId: parseInt(userId), phone })
    });
    const data = await res.json();
    btn.disabled = false;
    btn.innerText = 'طلب الرمز';

    if (res.ok && data.code) {
      document.getElementById('pair-code-display').innerText = data.code;
      resDiv.classList.remove('hidden');
      addLog('SUCCESS', `تم توليد رمز الاقتران للمستخدم #${userId}: ${data.code}`, 'WhatsAppEngine');
      showToast('✨ رمز الاقتران جاهز!', `أدخل الرمز (${data.code}) في تطبيق الواتساب عبر الأجهزة المرتبطة.`, 'info');
      fetchData();
    } else {
      errDiv.innerText = data.error || data.message || 'فشل في جلب رمز الاقتران';
      errDiv.classList.remove('hidden');
    }
  } catch (e) {
    btn.disabled = false;
    btn.innerText = 'طلب الرمز';
    errDiv.innerText = 'حدث خطأ أثناء الاتصال بالخادم';
    errDiv.classList.remove('hidden');
  }
}

async function triggerUnlink(userId) {
  if (!confirm(`هل أنت متأكد من رغبتك في فصل جلسة الواتساب للمستخدم #${userId}؟`)) return;

  try {
    const res = await authFetch(`${API_BASE}/WhatsApp/disconnect?targetUserIdParam=${userId}`, {
      method: 'POST'
    });
    if (res.ok) {
      showToast('تم فصل الجلسة', `تم فصل اتصال الواتساب للمستخدم #${userId} بنجاح`, 'success');
      addLog('INFO', `تم فصل جلسة الواتساب للمستخدم #${userId}`, 'WhatsAppEngine');
      fetchData();
    }
  } catch (e) {
    alert('حدث خطأ أثناء فصل الجلسة');
  }
}

// Auto Refresh Timer (Only polls when authenticated)
setInterval(() => {
  if (isUserLoggedIn()) {
    fetchData();
    if (currentTab === 'subscriptions') {
      fetchSubscriptionStats();
    }
  }
}, 3000);

// Initialize on page load
window.addEventListener('DOMContentLoaded', () => {
  applyTheme(getSavedTheme());

  // Restore sidebar collapse state
  if (localStorage.getItem('eqfal_sidebar_collapsed') === 'true' && window.innerWidth >= 1024) {
    const sidebar = document.getElementById('sidebar');
    const toggleIcon = document.getElementById('sidebar-toggle-icon');
    if (sidebar) sidebar.classList.add('collapsed');
    if (toggleIcon) toggleIcon.textContent = 'menu';
  }

  checkAuthOnLoad();
});

/* ==========================================================================
   SUBSCRIPTION & MONETIZATION MANAGEMENT MODULES
   ========================================================================== */

// 1. Subscriptions Management
let cachedSubscriptions = [];
let currentSubFilter = 'ALL';
let subSearchTimer = null;

async function fetchSubscriptionStats() {
  try {
    const res = await authFetch(`${API_BASE}/Subscriptions/admin/stats`);
    if (res.ok) {
      const data = await res.json();
      const elActive = document.getElementById('subs-stat-active');
      const elTrial = document.getElementById('subs-stat-trial');
      const elGrace = document.getElementById('subs-stat-grace');
      const elExpired = document.getElementById('subs-stat-expired');
      if (elActive) elActive.innerText = data.activeCount ?? data.active ?? 0;
      if (elTrial) elTrial.innerText = data.trialCount ?? data.trial ?? 0;
      if (elGrace) elGrace.innerText = data.graceCount ?? data.grace ?? 0;
      if (elExpired) elExpired.innerText = data.expiredCount ?? data.expired ?? 0;

      const activeTotal = (data.activeCount ?? data.active ?? 0) + (data.trialCount ?? data.trial ?? 0);
      const badge = document.getElementById('subs-active-badge');
      if (badge) badge.innerText = activeTotal;
    }
  } catch (e) {
    // Silent fail if unauthorized or offline
  }
}

async function fetchSubscriptions() {
  try {
    const res = await authFetch(`${API_BASE}/Subscriptions/admin?pageSize=500`);
    if (res.ok) {
      const json = await res.json();
      cachedSubscriptions = Array.isArray(json) ? json : (json.data || []);
      renderSubscriptionsTable();
    } else {
      const tbody = document.getElementById('subscriptions-table-body');
      if (tbody) tbody.innerHTML = `<tr><td colspan="7" class="p-8 text-center text-amber-500 font-medium">⚠️ تعذر تحميل بيانات المشتركين (${res.status}). يرجى التأكد من تشغيل خادم الـ API.</td></tr>`;
    }
  } catch (e) {
    console.error('fetchSubscriptions error:', e);
    const tbody = document.getElementById('subscriptions-table-body');
    if (tbody) tbody.innerHTML = `<tr><td colspan="7" class="p-8 text-center text-rose-500 font-medium">⚠️ تعذر الاتصال بالخادم لجلب بيانات المشتركين.</td></tr>`;
  }
}

function filterSubsStatus(status) {
  currentSubFilter = status;
  ['ALL', 'Active', 'Trial', 'Grace', 'Expired'].forEach(s => {
    const btn = document.getElementById(`sub-filter-${s}`);
    if (btn) {
      if (s === status) {
        btn.className = 'px-3 py-1.5 rounded-lg bg-indigo-600 text-white font-bold transition shadow-sm';
      } else {
        btn.className = 'px-3 py-1.5 rounded-lg bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-300 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 transition';
      }
    }
  });
  renderSubscriptionsTable();
}

function debounceSubsSearch() {
  clearTimeout(subSearchTimer);
  subSearchTimer = setTimeout(renderSubscriptionsTable, 250);
}

function renderSubscriptionsTable() {
  const tbody = document.getElementById('subscriptions-table-body');
  if (!tbody) return;

  const search = (document.getElementById('subs-search-input')?.value || '').trim().toLowerCase();

  let filtered = cachedSubscriptions.filter(s => {
    if (currentSubFilter !== 'ALL' && s.status !== currentSubFilter) return false;
    if (search) {
      const uName = (s.fullName || '').toLowerCase();
      const uEmail = (s.email || s.userName || '').toLowerCase();
      const uPhone = (s.phone || s.userPhone || '').toLowerCase();
      const pName = (s.planType || s.planName || '').toLowerCase();
      if (!uName.includes(search) && !uEmail.includes(search) && !uPhone.includes(search) && !pName.includes(search)) return false;
    }
    return true;
  });

  if (filtered.length === 0) {
    tbody.innerHTML = `<tr><td colspan="9" class="p-8 text-center text-slate-500 font-semibold">لا توجد اشتراكات مطابقة للبحث</td></tr>`;
    return;
  }

  const cycleNames = { 1: 'شهري', 2: 'ربع سنوي (3 شهور)', 3: 'نصف سنوي (6 شهور)', 4: 'سنوي' };

  tbody.innerHTML = filtered.map(s => {
    let badge = '';
    if (s.status === 'Active') {
      badge = '<span class="bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30 px-2.5 py-1 rounded-full font-bold">نشط 🟢</span>';
    } else if (s.status === 'Trial') {
      badge = '<span class="bg-indigo-500/15 text-indigo-600 dark:text-indigo-400 border border-indigo-500/30 px-2.5 py-1 rounded-full font-bold">تجريبي 🔵</span>';
    } else if (s.status === 'Grace') {
      badge = '<span class="bg-amber-500/15 text-amber-600 dark:text-amber-400 border border-amber-500/30 px-2.5 py-1 rounded-full font-bold">سماح 🟡</span>';
    } else {
      badge = '<span class="bg-rose-500/15 text-rose-600 dark:text-rose-400 border border-rose-500/30 px-2.5 py-1 rounded-full font-bold">منتهي 🔴</span>';
    }

    const expiryStr = s.expiresAt ? new Date(s.expiresAt).toLocaleDateString('ar-LY', { year: 'numeric', month: 'short', day: 'numeric' }) : '—';
    const cycleStr = cycleNames[s.billingCycle] || (s.isTrial ? 'فترة تجريبية' : '—');
    const fullName = (s.fullName && s.fullName.trim()) ? s.fullName.trim() : '—';
    const username = s.email || s.userName || s.username || '—';
    const displayName = fullName !== '—' ? fullName : username;
    const displayPhone = s.phone || s.userPhone || '—';
    const displayPlan = s.planType || s.planName || 'الباقة الأساسية';
    const subTargetId = s.subscriptionId || s.id || 0;

    return `
      <tr class="hover:bg-slate-50 dark:hover:bg-slate-900/50 transition">
        <td class="p-3.5 font-mono text-slate-500 font-semibold">#${s.userId}</td>
        <td class="p-3.5 font-bold text-slate-900 dark:text-white">
          ${fullName}
        </td>
        <td class="p-3.5 font-mono text-indigo-600 dark:text-indigo-400 font-semibold">
          ${username}
        </td>
        <td class="p-3.5 font-mono text-slate-700 dark:text-slate-300 font-medium">${displayPhone}</td>
        <td class="p-3.5 font-bold text-emerald-600 dark:text-emerald-400">${displayPlan}</td>
        <td class="p-3.5 text-slate-700 dark:text-slate-300 font-medium">${cycleStr}</td>
        <td class="p-3.5 font-mono text-amber-600 dark:text-amber-400 font-semibold">${expiryStr}</td>
        <td class="p-3.5 text-center">${badge}</td>
        <td class="p-3.5 text-center">
          <button onclick="openExtendModal(${subTargetId}, '${displayName.replace(/'/g, "\\'")}', '${expiryStr}')" class="px-2.5 py-1 rounded-lg bg-emerald-500/10 hover:bg-emerald-500/20 text-emerald-700 dark:text-emerald-300 border border-emerald-500/30 text-xs flex items-center gap-1 mx-auto transition font-bold" title="تمديد الاشتراك يدوياً">
            <span class="material-symbols-outlined text-xs">more_time</span> تمديد
          </button>
        </td>
      </tr>
    `;
  }).join('');
}

function openExtendModal(subId, userName, currentExpiry) {
  document.getElementById('extend-modal').classList.remove('hidden');
  document.getElementById('extend-sub-id').value = subId;
  document.getElementById('extend-user-name').innerText = userName;
  document.getElementById('extend-current-expiry').innerText = currentExpiry;
  document.getElementById('extend-days').value = 30;
  document.getElementById('extend-reason').value = '';
  document.getElementById('extend-error').classList.add('hidden');
}

function closeExtendModal() {
  document.getElementById('extend-modal').classList.add('hidden');
}

function setExtendDays(days) {
  document.getElementById('extend-days').value = days;
}

async function submitExtendSubscription() {
  const subId = document.getElementById('extend-sub-id').value;
  const days = parseInt(document.getElementById('extend-days').value);
  const reason = document.getElementById('extend-reason').value.trim();
  const errDiv = document.getElementById('extend-error');
  const btn = document.getElementById('btn-submit-extend');

  if (!days || days <= 0) {
    errDiv.innerText = 'يرجى إدخال عدد أيام صحيح (أكبر من 0)';
    errDiv.classList.remove('hidden');
    return;
  }

  errDiv.classList.add('hidden');
  btn.disabled = true;
  btn.innerText = 'جاري التمديد...';

  try {
    const res = await authFetch(`${API_BASE}/Subscriptions/${subId}/extend`, {
      method: 'POST',
      body: JSON.stringify({ additionalDays: days, reason: reason || 'تمديد إداري يدوي' })
    });
    const data = await res.json();
    btn.disabled = false;
    btn.innerText = 'تأكيد التمديد';

    if (res.ok && data.success) {
      closeExtendModal();
      showToast('تم تمديد الاشتراك بنجاح!', `تم إضافة ${days} يوماً للاشتراك بنجاح.`, 'success');
      addLog('SUCCESS', `تمديد اشتراك #${subId} بمقدار ${days} يوماً`, 'SubscriptionAdmin');
      fetchSubscriptions();
      fetchSubscriptionStats();
    } else {
      errDiv.innerText = data.message || 'فشل التمديد';
      errDiv.classList.remove('hidden');
    }
  } catch (e) {
    btn.disabled = false;
    btn.innerText = 'تأكيد التمديد';
    errDiv.innerText = 'حدث خطأ أثناء الاتصال بالخادم';
    errDiv.classList.remove('hidden');
  }
}

// 2. Pricing Plans Management
let currentPlanId = null;
let cachedProPlan = null;

async function fetchPricingPlans() {
  try {
    const res = await authFetch(`${API_BASE}/SubscriptionPlans`);
    if (res.ok) {
      const json = await res.json();
      const plans = Array.isArray(json) ? json : (json.data || json.plans || []);
      const pro = plans.find(p => !p.isTrialPlan && !p.isTrial) || plans[0];
      if (pro) {
        cachedProPlan = pro;
        currentPlanId = pro.id;
        if (pro.prices && Array.isArray(pro.prices)) {
          pro.prices.forEach(pr => {
            const cycle = pr.billingCycle;
            const price = pr.price;
            if (cycle === 1 && document.getElementById('plan-price-monthly')) document.getElementById('plan-price-monthly').value = price;
            if (cycle === 4 && document.getElementById('plan-price-yearly')) document.getElementById('plan-price-yearly').value = price;
          });
        }
      }
    }
  } catch (e) {
    console.error('fetchPricingPlans error:', e);
  }
}

async function savePlanPrices() {
  if (!currentPlanId) {
    alert('لم يتم تحميل الخطة بعد، يرجى المحاولة بعد قليل');
    return;
  }

  const pMonthly = parseFloat(document.getElementById('plan-price-monthly')?.value) || 50;
  const pYearly = parseFloat(document.getElementById('plan-price-yearly')?.value) || 420;

  const btn = document.getElementById('btn-save-prices');
  btn.disabled = true;
  btn.innerHTML = '<span class="material-symbols-outlined text-base animate-spin">sync</span> جاري الحفظ...';

  try {
    const payload = {
      name: cachedProPlan?.name || "Eqfal Pro",
      nameAr: cachedProPlan?.nameAr || "إقفال برو",
      descriptionAr: cachedProPlan?.descriptionAr || "خطة كاملة بدون حدود على القيود أو المحادثات",
      isTrialPlan: false,
      trialDurationDays: null,
      isPopular: true,
      isActive: true,
      sortOrder: cachedProPlan?.sortOrder || 1,
      themeKey: cachedProPlan?.themeKey || "emerald",
      features: cachedProPlan?.features || ["ربط مباشر مع الواتساب", "بدون حدود على القيود اليومية", "كشوفات حساب وتقارير فورية"],
      prices: [
        { billingCycle: 1, price: pMonthly, durationDays: 30, isActive: true },
        { billingCycle: 4, price: pYearly, durationDays: 365, isActive: true }
      ]
    };

    const res = await authFetch(`${API_BASE}/SubscriptionPlans/${currentPlanId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    const data = await res.json().catch(() => ({}));
    btn.disabled = false;
    btn.innerHTML = '<span class="material-symbols-outlined text-base">save</span> حفظ وتحديث الأسعار (شهري وسنوي)';

    if (res.ok && (data.success || data.id)) {
      showToast('تم حفظ الأسعار بنجاح!', 'تم تحديث أسعار الباقة (شهري وسنوي) بنجاح.', 'success');
      addLog('SUCCESS', `تحديث أسعار الخطة #${currentPlanId} (شهري: ${pMonthly}, سنوي: ${pYearly})`, 'PricingAdmin');
      fetchPricingPlans();
    } else {
      alert(data.message || `فشل حفظ الأسعار (رمز الاستجابة: ${res.status})`);
    }
  } catch (e) {
    console.error('savePlanPrices error:', e);
    btn.disabled = false;
    btn.innerHTML = '<span class="material-symbols-outlined text-base">save</span> حفظ وتحديث الأسعار (شهري وسنوي)';
    alert('حدث خطأ في الاتصال بالخادم أثناء حفظ الأسعار: ' + (e.message || ''));
  }
}

async function fetchSubscriptionSettings() {
  try {
    const res = await authFetch(`${API_BASE}/Subscriptions/settings`);
    if (res.ok) {
      const data = await res.json();
      if (data.gracePeriodDays !== undefined) {
        document.getElementById('settings-grace-days').value = data.gracePeriodDays;
      }
      if (data.trialDurationDays !== undefined) {
        document.getElementById('settings-trial-days').value = data.trialDurationDays;
      }
    }
  } catch (e) {}
}

async function saveSubscriptionSettings() {
  const graceDays = parseInt(document.getElementById('settings-grace-days').value);
  const trialDays = parseInt(document.getElementById('settings-trial-days').value);
  try {
    const res = await authFetch(`${API_BASE}/Subscriptions/settings`, {
      method: 'PUT',
      body: JSON.stringify({ gracePeriodDays: graceDays, trialDurationDays: trialDays })
    });
    if (res.ok) {
      showToast('تم حفظ إعدادات المنظومة!', `فترة السماح أصبحت ${graceDays} أيام.`, 'success');
      addLog('SUCCESS', `تحديث فترة السماح إلى ${graceDays} أيام وفترة التجربة إلى ${trialDays} أيام`, 'SystemSettings');
    }
  } catch (e) {
    alert('حدث خطأ أثناء حفظ الإعدادات');
  }
}

// 3. Payments & Revenue Management
let cachedPayments = [];

async function fetchPaymentsSummary() {
  try {
    const res = await authFetch(`${API_BASE}/SubscriptionPayments/admin/summary`);
    if (res.ok) {
      const data = await res.json();
      const revEl = document.getElementById('pay-stat-total-revenue');
      const setEl = document.getElementById('pay-stat-settled-count');
      const penEl = document.getElementById('pay-stat-pending-count');
      const faiEl = document.getElementById('pay-stat-failed-count');
      if (revEl) revEl.innerText = (data.totalRevenue || 0).toLocaleString('ar-LY', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
      if (setEl) setEl.innerText = data.settledCount || 0;
      if (penEl) penEl.innerText = data.pendingCount || 0;
      if (faiEl) faiEl.innerText = data.failedCount || 0;
    }
  } catch (e) {
    console.error('fetchPaymentsSummary error:', e);
  }
}

async function fetchPayments() {
  try {
    const res = await authFetch(`${API_BASE}/SubscriptionPayments/admin`);
    if (res.ok) {
      const json = await res.json();
      cachedPayments = Array.isArray(json) ? json : (json.data || []);
      renderPaymentsTable();
    } else {
      const tbody = document.getElementById('payments-table-body');
      if (tbody) tbody.innerHTML = `<tr><td colspan="9" class="p-8 text-center text-amber-500 font-medium">⚠️ تعذر تحميل سجل المدفوعات (${res.status}). يرجى التأكد من تشغيل خادم الـ API.</td></tr>`;
    }
  } catch (e) {
    console.error('fetchPayments error:', e);
    const tbody = document.getElementById('payments-table-body');
    if (tbody) tbody.innerHTML = `<tr><td colspan="9" class="p-8 text-center text-rose-500 font-medium">⚠️ تعذر الاتصال بالخادم لجلب سجل المدفوعات.</td></tr>`;
  }
}

function renderPaymentsTable() {
  const tbody = document.getElementById('payments-table-body');
  if (!tbody) return;

  if (cachedPayments.length === 0) {
    tbody.innerHTML = `<tr><td colspan="9" class="p-8 text-center text-slate-500 font-semibold">لا توجد عمليات دفع مسجلة حتى الآن</td></tr>`;
    return;
  }

  tbody.innerHTML = cachedPayments.map(p => {
    let stBadge = '';
    if (p.status === 'Settled') {
      stBadge = '<span class="bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30 px-2.5 py-0.5 rounded-full font-bold">مسدد 🟢</span>';
    } else if (p.status === 'Pending') {
      stBadge = '<span class="bg-amber-500/15 text-amber-600 dark:text-amber-400 border border-amber-500/30 px-2.5 py-0.5 rounded-full font-bold">معلق 🟡</span>';
    } else if (p.status === 'Failed') {
      stBadge = '<span class="bg-rose-500/15 text-rose-600 dark:text-rose-400 border border-rose-500/30 px-2.5 py-0.5 rounded-full font-bold">فاشل 🔴</span>';
    } else {
      stBadge = `<span class="text-slate-500 dark:text-slate-400 font-mono font-medium">${p.status}</span>`;
    }

    const dateStr = p.createdAt ? new Date(p.createdAt).toLocaleString('ar-LY') : '—';
    const fullName = (p.fullName && p.fullName.trim()) ? p.fullName.trim() : (p.userName || 'مستخدم #' + p.userId);
    const username = p.username || p.userEmail || p.userPhone || '—';

    return `
      <tr class="hover:bg-slate-50 dark:hover:bg-slate-900/50 transition">
        <td class="p-3.5 font-mono text-slate-500 dark:text-slate-400 font-bold">#${p.id}</td>
        <td class="p-3.5 font-bold text-slate-900 dark:text-white">${fullName}</td>
        <td class="p-3.5 font-mono text-indigo-600 dark:text-indigo-400 font-semibold">${username}</td>
        <td class="p-3.5 font-mono text-emerald-600 dark:text-emerald-400 font-bold">${(p.amount || 0).toFixed(2)} د.ل</td>
        <td class="p-3.5 text-slate-700 dark:text-slate-300 font-medium">${p.paymentGateway || 'EzonePay'}</td>
        <td class="p-3.5 font-mono text-amber-600 dark:text-amber-300 font-bold">${p.discountCode || '—'}</td>
        <td class="p-3.5 font-mono text-slate-500 dark:text-slate-400 text-[11px] truncate max-w-[120px]" title="${p.gatewayReferenceId || ''}">${p.gatewayReferenceId || '—'}</td>
        <td class="p-3.5 font-mono text-slate-600 dark:text-slate-300">${dateStr}</td>
        <td class="p-3.5 text-center">${stBadge}</td>
      </tr>
    `;
  }).join('');
}

// 4. Marketers & Discount Codes
let cachedMarketers = [];
let cachedDiscounts = [];

async function fetchMarketers() {
  try {
    const res = await authFetch(`${API_BASE}/Marketers`);
    if (res.ok) {
      const json = await res.json();
      cachedMarketers = Array.isArray(json) ? json : (json.data || []);
      renderMarketersTable();
    } else {
      const tbody = document.getElementById('marketers-table-body');
      if (tbody) tbody.innerHTML = `<tr><td colspan="8" class="p-8 text-center text-amber-500 font-medium">⚠️ تعذر تحميل بيانات المسوقين (${res.status}). يرجى التأكد من تشغيل خادم الـ API.</td></tr>`;
    }
  } catch (e) {
    console.error('fetchMarketers error:', e);
    const tbody = document.getElementById('marketers-table-body');
    if (tbody) tbody.innerHTML = `<tr><td colspan="8" class="p-8 text-center text-rose-500 font-medium">⚠️ تعذر الاتصال بالخادم لجلب بيانات المسوقين.</td></tr>`;
  }
}

function renderMarketersTable() {
  const tbody = document.getElementById('marketers-table-body');
  if (!tbody) return;

  if (!cachedMarketers || cachedMarketers.length === 0) {
    tbody.innerHTML = `<tr><td colspan="8" class="p-8 text-center text-slate-500 font-semibold">لا يوجد مسوقون مسجلون حالياً</td></tr>`;
    return;
  }

  tbody.innerHTML = cachedMarketers.map(m => {
    const isPercent = m.commissionType === 0 || m.commissionType === 'Percentage';
    const commDesc = isPercent ? `${m.commissionValue}%` : `${m.commissionValue} د.ل (ثابت)`;
    const phone = m.phone || m.phoneNumber || '—';
    const curBalance = m.currentBalance !== undefined ? m.currentBalance : (m.balance || 0);
    const totEarned = m.totalEarned !== undefined ? m.totalEarned : (m.totalAccrued || 0);
    const totPaid = m.totalPaid || 0;
    const safeName = (m.name || '').replace(/'/g, "\\'");

    let codesHtml = '—';
    if (Array.isArray(m.discountCodes) && m.discountCodes.length > 0) {
      codesHtml = `<div class="flex flex-wrap gap-1 max-w-[220px]">` + m.discountCodes.map(c => {
        const isAct = c.isActive && (c.maxUses === null || c.timesUsed < c.maxUses);
        const badgeClass = isAct 
          ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/30' 
          : 'bg-rose-500/10 text-rose-500 border-rose-500/30 line-through opacity-75';
        return `<span class="px-1.5 py-0.5 rounded border text-[11px] font-mono font-bold ${badgeClass}" title="${c.timesUsed}/${c.maxUses || '∞'} استخدام">${c.code}</span>`;
      }).join('') + `</div>`;
    } else if (m.discountCode) {
      codesHtml = `<span class="font-mono text-emerald-600 dark:text-emerald-400 font-bold tracking-wider">${m.discountCode}</span>`;
    }

    return `
      <tr class="hover:bg-slate-50 dark:hover:bg-slate-900/50 transition">
        <td class="p-3.5 font-bold text-slate-900 dark:text-white">
          <div class="flex items-center gap-1.5">
            <span>${m.name || '—'}</span>
            ${m.isActive === false ? '<span class="text-[10px] px-1 rounded bg-rose-500/10 text-rose-400 border border-rose-500/20">معطل</span>' : ''}
          </div>
        </td>
        <td class="p-3.5 font-mono text-slate-700 dark:text-slate-300">${phone}</td>
        <td class="p-3.5">${codesHtml}</td>
        <td class="p-3.5 text-slate-700 dark:text-slate-300 font-medium">${commDesc}</td>
        <td class="p-3.5 font-mono font-bold text-amber-600 dark:text-amber-400">${Number(curBalance).toFixed(2)} د.ل</td>
        <td class="p-3.5 font-mono text-emerald-600 dark:text-emerald-400 font-bold">${Number(totEarned).toFixed(2)} د.ل</td>
        <td class="p-3.5 font-mono text-slate-500 dark:text-slate-400">${Number(totPaid).toFixed(2)} د.ل</td>
        <td class="p-3.5 text-center flex items-center justify-center gap-1.5 flex-wrap">
          <button onclick="openEditMarketerModal('${m.id}')" class="px-2 py-1 rounded bg-indigo-500/15 hover:bg-indigo-500/25 text-indigo-700 dark:text-indigo-300 border border-indigo-500/30 text-xs font-bold transition flex items-center gap-1" title="تعديل المسوق وإدارة الأكواد">
            <span class="material-symbols-outlined text-xs">edit</span> تعديل / كود
          </button>
          <button onclick="openPayoutModal('${m.id}', '${safeName}', ${curBalance})" class="px-2 py-1 rounded bg-amber-500/15 hover:bg-amber-500/25 text-amber-700 dark:text-amber-300 border border-amber-500/30 text-xs font-bold transition" title="صرف دفعة">
            صرف دفعة
          </button>
          <button onclick="openMarketerLedgerModal('${m.id}', '${safeName}')" class="px-2 py-1 rounded bg-slate-200 dark:bg-slate-800 hover:bg-slate-300 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-300 text-xs font-medium transition" title="كشف الحساب">
            كشف حساب
          </button>
          <button onclick="confirmDeleteMarketer('${m.id}', '${safeName}')" class="px-2 py-1 rounded bg-rose-500/15 hover:bg-rose-500/25 text-rose-600 dark:text-rose-400 border border-rose-500/30 text-xs font-bold transition flex items-center gap-1" title="حذف المسوق">
            <span class="material-symbols-outlined text-xs">delete</span> حذف
          </button>
        </td>
      </tr>
    `;
  }).join('');
}

function openAddMarketerModal() {
  document.getElementById('add-marketer-modal').classList.remove('hidden');
  document.getElementById('m-name').value = '';
  document.getElementById('m-phone').value = '';
  if (document.getElementById('m-max-uses')) document.getElementById('m-max-uses').value = '';
  if (document.getElementById('m-expiry')) document.getElementById('m-expiry').value = '';
  generateRandomCode();
  document.getElementById('add-marketer-error').classList.add('hidden');
}

function closeAddMarketerModal() {
  document.getElementById('add-marketer-modal').classList.add('hidden');
}

function generateRandomCode() {
  const rand = Math.floor(1000 + Math.random() * 9000);
  document.getElementById('m-code').value = `EQF-${rand}`;
}

async function submitAddMarketer() {
  const name = document.getElementById('m-name').value.trim();
  const phone = document.getElementById('m-phone').value.trim();
  const code = document.getElementById('m-code').value.trim().toUpperCase();
  const discountType = parseInt(document.getElementById('m-discount-type').value);
  const discountValue = parseFloat(document.getElementById('m-discount-value').value);
  const commType = parseInt(document.getElementById('m-comm-type').value);
  const commValue = parseFloat(document.getElementById('m-comm-value').value);
  const maxUsesVal = document.getElementById('m-max-uses')?.value;
  const maxUsages = maxUsesVal ? parseInt(maxUsesVal) : null;
  const expiryVal = document.getElementById('m-expiry')?.value;
  const expiresAt = expiryVal ? new Date(expiryVal).toISOString() : null;
  const errDiv = document.getElementById('add-marketer-error');
  const btn = document.getElementById('btn-submit-marketer');

  if (!name || !code) {
    errDiv.innerText = 'يرجى إدخال اسم المسوق وكود الخصم';
    errDiv.classList.remove('hidden');
    return;
  }

  errDiv.classList.add('hidden');
  btn.disabled = true;
  btn.innerText = 'جاري الحفظ...';

  try {
    const res = await authFetch(`${API_BASE}/Marketers`, {
      method: 'POST',
      body: JSON.stringify({
        name,
        phone,
        phoneNumber: phone,
        discountCode: code,
        discountType,
        discountValue,
        commissionType: commType,
        commissionValue: commValue,
        maxUses: maxUsages,
        maxUsages: maxUsages,
        expiresAt: expiresAt
      })
    });
    const data = await res.json();
    btn.disabled = false;
    btn.innerText = 'حفظ وإصدار الكود';

    if (res.ok && (data.id || data.success)) {
      closeAddMarketerModal();
      showToast('تمت إضافة المسوق بنجاح!', `تم تفعيل المسوق والكود ${code} بنجاح.`, 'success');
      addLog('SUCCESS', `إضافة مسوق جديد: ${name} بكود ${code}`, 'MarketerAdmin');
      fetchMarketers();
      fetchDiscountCodes();
    } else {
      errDiv.innerText = data.message || 'فشلت إضافة المسوق';
      errDiv.classList.remove('hidden');
    }
  } catch (e) {
    btn.disabled = false;
    btn.innerText = 'حفظ وإصدار الكود';
    errDiv.innerText = 'حدث خطأ في الاتصال بالخادم';
    errDiv.classList.remove('hidden');
  }
}

function openPayoutModal(marketerId, name, balance) {
  document.getElementById('payout-modal').classList.remove('hidden');
  document.getElementById('payout-marketer-id').value = marketerId;
  document.getElementById('payout-marketer-name').innerText = name;
  document.getElementById('payout-current-balance').innerText = balance.toFixed(2) + ' د.ل';
  document.getElementById('payout-amount').value = balance > 0 ? balance : '';
  document.getElementById('payout-notes').value = '';
  document.getElementById('payout-error').classList.add('hidden');
}

function closePayoutModal() {
  document.getElementById('payout-modal').classList.add('hidden');
}

async function submitMarketerPayout() {
  const marketerId = document.getElementById('payout-marketer-id').value;
  const amount = parseFloat(document.getElementById('payout-amount').value);
  const notes = document.getElementById('payout-notes').value.trim();
  const errDiv = document.getElementById('payout-error');
  const btn = document.getElementById('btn-submit-payout');

  if (!amount || amount <= 0) {
    errDiv.innerText = 'يرجى إدخال مبلغ صحيح للصرف';
    errDiv.classList.remove('hidden');
    return;
  }

  errDiv.classList.add('hidden');
  btn.disabled = true;
  btn.innerText = 'جاري التسجيل...';

  try {
    const res = await authFetch(`${API_BASE}/Marketers/${marketerId}/payout`, {
      method: 'POST',
      body: JSON.stringify({ amount, notes: notes || 'صرف عمولة نقدي' })
    });
    const data = await res.json();
    btn.disabled = false;
    btn.innerText = 'تسجيل الصرف وترحيل القيد';

    if (res.ok && data.success) {
      closePayoutModal();
      showToast('تم تسجيل الصرف بنجاح!', `تم ترحيل قيد صرف بقيمة ${amount} د.ل للمسوق.`, 'success');
      addLog('SUCCESS', `صرف عمولة بقيمة ${amount} د.ل للمسوق #${marketerId}`, 'MarketerPayout');
      fetchMarketers();
    } else {
      errDiv.innerText = data.message || 'فشل تسجيل الصرف';
      errDiv.classList.remove('hidden');
    }
  } catch (e) {
    btn.disabled = false;
    btn.innerText = 'تسجيل الصرف وترحيل القيد';
    errDiv.innerText = 'حدث خطأ أثناء الاتصال بالخادم';
    errDiv.classList.remove('hidden');
  }
}

async function openMarketerLedgerModal(marketerId, name) {
  document.getElementById('marketer-ledger-modal').classList.remove('hidden');
  document.getElementById('ledger-modal-title').innerText = `كشف حساب المسوق: ${name}`;
  const tbody = document.getElementById('marketer-ledger-tbody');
  tbody.innerHTML = `<tr><td colspan="6" class="p-6 text-center text-slate-500">جاري تحميل كشف الحساب...</td></tr>`;

  try {
    const res = await authFetch(`${API_BASE}/Marketers/${marketerId}/ledger`);
    if (res.ok) {
      const json = await res.json();
      const txs = Array.isArray(json) ? json : (json.data || []);
      if (txs.length === 0) {
        tbody.innerHTML = `<tr><td colspan="6" class="p-6 text-center text-slate-500">لا توجد حركات مسجلة لهذا المسوق بعد</td></tr>`;
        return;
      }
      tbody.innerHTML = txs.map(t => {
        const typeBadge = t.type === 'Accrual' || t.type === 0
          ? '<span class="text-emerald-600 dark:text-emerald-400 font-bold">+استحقاق (Accrual)</span>'
          : '<span class="text-rose-600 dark:text-rose-400 font-bold">-صرف (Payout)</span>';
        const dateStr = t.createdAt ? new Date(t.createdAt).toLocaleString('ar-LY') : '—';
        const amt = Number(t.amount || 0).toFixed(2);
        const bal = Number(t.balanceAfter || 0).toFixed(2);
        return `
          <tr class="hover:bg-slate-50 dark:hover:bg-slate-900/50">
            <td class="p-2.5 font-mono text-slate-500 dark:text-slate-400">#${t.id}</td>
            <td class="p-2.5">${typeBadge}</td>
            <td class="p-2.5 font-bold text-slate-900 dark:text-white">${amt} د.ل</td>
            <td class="p-2.5 text-amber-600 dark:text-amber-400 font-bold">${bal} د.ل</td>
            <td class="p-2.5 font-mono text-slate-600 dark:text-slate-400">${dateStr}</td>
            <td class="p-2.5 text-slate-700 dark:text-slate-300 font-sans">${t.notes || '—'}</td>
          </tr>
        `;
      }).join('');
    }
  } catch (e) {
    console.error('openMarketerLedgerModal error:', e);
    tbody.innerHTML = `<tr><td colspan="6" class="p-6 text-center text-rose-400">فشل في تحميل كشف الحساب</td></tr>`;
  }
}

function closeMarketerLedgerModal() {
  document.getElementById('marketer-ledger-modal').classList.add('hidden');
}

async function fetchDiscountCodes() {
  try {
    const res = await authFetch(`${API_BASE}/DiscountCodes`);
    if (res.ok) {
      const json = await res.json();
      cachedDiscounts = Array.isArray(json) ? json : (json.data || []);
      renderDiscountsTable();
    } else {
      const tbody = document.getElementById('discounts-table-body');
      if (tbody) tbody.innerHTML = `<tr><td colspan="6" class="p-6 text-center text-amber-500 font-medium">⚠️ تعذر تحميل أكواد الخصم (${res.status}). يرجى التأكد من تشغيل خادم الـ API.</td></tr>`;
    }
  } catch (e) {
    console.error('fetchDiscountCodes error:', e);
    const tbody = document.getElementById('discounts-table-body');
    if (tbody) tbody.innerHTML = `<tr><td colspan="6" class="p-6 text-center text-rose-500 font-medium">⚠️ تعذر الاتصال بالخادم لجلب أكواد الخصم.</td></tr>`;
  }
}

function renderDiscountsTable() {
  const tbody = document.getElementById('discounts-table-body');
  if (!tbody) return;

  if (!cachedDiscounts || cachedDiscounts.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7" class="p-6 text-center text-slate-500 font-semibold">لا توجد أكواد خصم مسجلة</td></tr>`;
    return;
  }

  tbody.innerHTML = cachedDiscounts.map(d => {
    const isPercent = d.discountType === 0 || d.discountType === 'Percentage';
    const typeStr = isPercent ? 'نسبة مئوية' : 'مبلغ ثابت';
    const val = d.value !== undefined ? d.value : (d.discountValue || 0);
    const valStr = isPercent ? `${val}%` : `${val} د.ل`;
    const timesUsed = d.timesUsed !== undefined ? d.timesUsed : (d.usedCount || 0);
    const maxUses = (d.maxUses !== undefined && d.maxUses !== null) ? d.maxUses : ((d.maxUsages !== undefined && d.maxUsages !== null) ? d.maxUsages : 'غير محدود');
    const st = d.isActive ? '<span class="text-emerald-600 dark:text-emerald-400 font-bold">نشط 🟢</span>' : '<span class="text-rose-600 dark:text-rose-400 font-bold">معطل 🔴</span>';

    return `
      <tr class="hover:bg-slate-50 dark:hover:bg-slate-900/50 transition font-mono">
        <td class="p-3 font-bold text-amber-600 dark:text-amber-400 font-sans">${d.code}</td>
        <td class="p-3 text-slate-700 dark:text-slate-300 font-sans">${typeStr}</td>
        <td class="p-3 font-bold text-emerald-600 dark:text-emerald-400">${valStr}</td>
        <td class="p-3 text-slate-700 dark:text-slate-300">${timesUsed}</td>
        <td class="p-3 text-slate-500 dark:text-slate-400">${maxUses}</td>
        <td class="p-3 text-center">${st}</td>
        <td class="p-3 text-center">
          <button onclick="confirmDeleteDiscountCode('${d.id}', '${d.code}')" class="px-2 py-1 rounded bg-rose-500/15 hover:bg-rose-500/25 text-rose-600 dark:text-rose-400 border border-rose-500/30 text-xs font-bold transition inline-flex items-center gap-1" title="حذف الكود">
            <span class="material-symbols-outlined text-xs">delete</span> حذف
          </button>
        </td>
      </tr>
    `;
  }).join('');
}

function openAddDiscountModal() {
  document.getElementById('add-discount-modal').classList.remove('hidden');
  document.getElementById('disc-code').value = '';
  document.getElementById('disc-value').value = '10';
  document.getElementById('disc-max-uses').value = '';
  document.getElementById('disc-expiry').value = '';
  document.getElementById('add-disc-error').classList.add('hidden');
}

function closeAddDiscountModal() {
  document.getElementById('add-discount-modal').classList.add('hidden');
}

async function submitAddDiscountCode() {
  const code = document.getElementById('disc-code').value.trim().toUpperCase();
  const type = parseInt(document.getElementById('disc-type').value);
  const value = parseFloat(document.getElementById('disc-value').value);
  const maxUsesVal = document.getElementById('disc-max-uses').value;
  const maxUsages = maxUsesVal ? parseInt(maxUsesVal) : null;
  const expiryVal = document.getElementById('disc-expiry').value;
  const expiresAt = expiryVal ? new Date(expiryVal).toISOString() : null;
  const errDiv = document.getElementById('add-disc-error');
  const btn = document.getElementById('btn-submit-discount');

  if (!code || !value || value <= 0) {
    errDiv.innerText = 'يرجى إدخال كود الخصم وقيمة الخصم بشكل صحيح';
    errDiv.classList.remove('hidden');
    return;
  }

  errDiv.classList.add('hidden');
  btn.disabled = true;
  btn.innerText = 'جاري التفعيل...';

  try {
    const res = await authFetch(`${API_BASE}/DiscountCodes`, {
      method: 'POST',
      body: JSON.stringify({
        code,
        discountType: type,
        discountValue: value,
        maxUsages,
        expiresAt
      })
    });
    const data = await res.json();
    btn.disabled = false;
    btn.innerText = 'تفعيل كود الخصم';

    if (res.ok && (data.id || data.success)) {
      closeAddDiscountModal();
      showToast('تم تفعيل كود الخصم!', `كود ${code} أصبح جاهزاً للاستخدام الآن.`, 'success');
      addLog('SUCCESS', `إصدار كود خصم جديد: ${code} بقيمة ${value}`, 'DiscountAdmin');
      fetchDiscountCodes();
    } else {
      errDiv.innerText = data.message || 'فشل إنشاء كود الخصم';
      errDiv.classList.remove('hidden');
    }
  } catch (e) {
    btn.disabled = false;
    btn.innerText = 'تفعيل كود الخصم';
    errDiv.innerText = 'حدث خطأ في الاتصال بالخادم';
    errDiv.classList.remove('hidden');
  }
}

// ==================== حذف كود خصم ====================
async function confirmDeleteDiscountCode(id, code) {
  if (!confirm(`هل أنت متأكد من حذف كود الخصم "${code}" نهائياً؟`)) return;

  try {
    const res = await authFetch(`${API_BASE}/DiscountCodes/${id}`, { method: 'DELETE' });
    const data = await res.json();
    if (res.ok && data.success) {
      showToast('تم الحذف!', `تم حذف كود الخصم ${code} بنجاح.`, 'success');
      addLog('SUCCESS', `حذف كود الخصم ${code}`, 'DiscountAdmin');
      fetchDiscountCodes();
      fetchMarketers();
    } else {
      showToast('خطأ!', data.message || 'فشل حذف كود الخصم', 'error');
    }
  } catch (e) {
    showToast('خطأ!', 'حدث خطأ أثناء محاولة حذف كود الخصم', 'error');
  }
}

// ==================== حذف مسوق ====================
async function confirmDeleteMarketer(id, name) {
  if (!confirm(`هل أنت متأكد من حذف المسوق "${name}" نهائياً من المنظومة؟\n\nتنبيه: سيتم حذف جميع أكواد الخصم والحركات المالية التابعة له.`)) return;

  try {
    const res = await authFetch(`${API_BASE}/Marketers/${id}`, { method: 'DELETE' });
    const data = await res.json();
    if (res.ok && data.success) {
      showToast('تم الحذف!', `تم حذف المسوق "${name}" بنجاح.`, 'success');
      addLog('SUCCESS', `حذف المسوق ${name} (#${id})`, 'MarketerAdmin');
      fetchMarketers();
      fetchDiscountCodes();
    } else {
      showToast('خطأ!', data.message || 'فشل حذف المسوق', 'error');
    }
  } catch (e) {
    showToast('خطأ!', 'حدث خطأ أثناء محاولة حذف المسوق', 'error');
  }
}

// ==================== تعديل مسوق وإدارة أكواده ====================
function generateEditCode() {
  const rand = Math.floor(1000 + Math.random() * 9000);
  const mName = document.getElementById('edit-m-name')?.value?.trim() || '';
  let prefix = 'EQF';
  if (mName) {
    const clean = mName.replace(/[^\u0621-\u064Aa-zA-Z]/g, '');
    if (clean.length >= 2) prefix = clean.substring(0, Math.min(4, clean.length)).toUpperCase();
  }
  document.getElementById('edit-m-new-code').value = `${prefix}-${rand}`;
}

function openEditMarketerModal(marketerId) {
  const m = cachedMarketers.find(x => x.id === marketerId);
  if (!m) return;

  document.getElementById('edit-marketer-modal').classList.remove('hidden');
  document.getElementById('edit-m-id').value = m.id;
  document.getElementById('edit-m-name').value = m.name || '';
  document.getElementById('edit-m-phone').value = m.phone || m.phoneNumber || '';
  document.getElementById('edit-m-comm-type').value = (m.commissionType === 1 || m.commissionType === 'Fixed') ? '1' : '0';
  document.getElementById('edit-m-comm-value').value = m.commissionValue !== undefined ? m.commissionValue : 10;
  document.getElementById('edit-m-active').value = (m.isActive !== false) ? 'true' : 'false';
  
  // تصفير حقول الكود الجديد
  document.getElementById('edit-m-new-code').value = '';
  document.getElementById('edit-m-disc-type').value = '0';
  document.getElementById('edit-m-disc-value').value = '10';
  document.getElementById('edit-m-max-uses').value = '';
  document.getElementById('edit-m-expiry').value = '';
  const radioKeep = document.querySelector('input[name="edit-m-old-action"][value="keep"]');
  if (radioKeep) radioKeep.checked = true;
  document.getElementById('edit-marketer-error').classList.add('hidden');

  // عرض الأكواد الحالية للمسوق
  renderEditMarketerCodes(m);
}

function renderEditMarketerCodes(marketer) {
  const container = document.getElementById('edit-m-codes-list');
  const countEl = document.getElementById('edit-m-codes-count');
  if (!container) return;

  const codes = marketer.discountCodes || [];
  if (countEl) countEl.innerText = `${codes.length} كود`;

  if (codes.length === 0) {
    if (marketer.discountCode) {
      container.innerHTML = `
        <div class="flex items-center justify-between p-2 rounded-lg bg-slate-900 border border-slate-800">
          <div class="flex items-center gap-2">
            <span class="font-mono font-bold text-emerald-400 text-sm">${marketer.discountCode}</span>
            <span class="px-1.5 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-[10px]">كود رئيسي</span>
          </div>
        </div>
      `;
    } else {
      container.innerHTML = `<div class="p-3 text-center text-slate-500">لا توجد أكواد خصم مسجلة لهذا المسوق حالياً</div>`;
    }
    return;
  }

  container.innerHTML = codes.map(c => {
    const isAct = c.isActive && (c.maxUses === null || c.timesUsed < c.maxUses);
    const badge = isAct 
      ? '<span class="text-emerald-400 font-bold text-[10px] px-1.5 py-0.5 rounded bg-emerald-500/10 border border-emerald-500/20">شغال 🟢</span>'
      : '<span class="text-rose-400 font-bold text-[10px] px-1.5 py-0.5 rounded bg-rose-500/10 border border-rose-500/20">معطل / منتهي 🔴</span>';
    const uses = `${c.timesUsed || 0} / ${c.maxUses ? c.maxUses : 'غير محدود'}`;
    const discStr = c.discountType === 1 ? `${c.value} د.ل` : `${c.value}%`;
    const safeCode = (c.code || '').replace(/'/g, "\\'");

    return `
      <div class="flex items-center justify-between p-2 rounded-lg bg-slate-900 border border-slate-800">
        <div class="flex items-center gap-2 flex-wrap">
          <span class="font-mono font-bold text-emerald-400 text-sm">${c.code}</span>
          <span class="text-slate-400 font-mono text-[11px]">(خصم: ${discStr})</span>
          <span class="text-slate-400 font-mono text-[11px]">(استخدام: ${uses})</span>
          ${badge}
        </div>
        <button type="button" onclick="deleteCodeFromMarketer('${c.id}', '${safeCode}', '${marketer.id}')" class="px-2 py-1 rounded bg-rose-500/15 hover:bg-rose-500/25 text-rose-400 border border-rose-500/30 text-[11px] font-bold transition flex items-center gap-1 shrink-0" title="حذف الكود">
          <span class="material-symbols-outlined text-xs">delete</span> حذف
        </button>
      </div>
    `;
  }).join('');
}

function closeEditMarketerModal() {
  document.getElementById('edit-marketer-modal').classList.add('hidden');
}

async function deleteCodeFromMarketer(codeId, codeName, marketerId) {
  if (!confirm(`هل أنت متأكد من حذف كود الخصم "${codeName}"؟`)) return;

  try {
    const res = await authFetch(`${API_BASE}/DiscountCodes/${codeId}`, { method: 'DELETE' });
    const data = await res.json();
    if (res.ok && data.success) {
      showToast('تم الحذف!', `تم حذف الكود "${codeName}" بنجاح.`, 'success');
      await fetchMarketers();
      await fetchDiscountCodes();
      const updatedM = cachedMarketers.find(x => x.id === marketerId);
      if (updatedM) {
        renderEditMarketerCodes(updatedM);
      } else {
        closeEditMarketerModal();
      }
    } else {
      showToast('خطأ!', data.message || 'فشل حذف الكود', 'error');
    }
  } catch (e) {
    showToast('خطأ!', 'حدث خطأ أثناء حذف الكود', 'error');
  }
}

async function submitEditMarketer() {
  const marketerId = document.getElementById('edit-m-id').value;
  const name = document.getElementById('edit-m-name').value.trim();
  const phone = document.getElementById('edit-m-phone').value.trim();
  const commType = parseInt(document.getElementById('edit-m-comm-type').value);
  const commValue = parseFloat(document.getElementById('edit-m-comm-value').value);
  const isActive = document.getElementById('edit-m-active').value === 'true';

  const newCode = document.getElementById('edit-m-new-code').value.trim().toUpperCase();
  const discType = parseInt(document.getElementById('edit-m-disc-type').value);
  const discValue = parseFloat(document.getElementById('edit-m-disc-value').value);
  const maxUsesVal = document.getElementById('edit-m-max-uses').value;
  const maxUses = maxUsesVal ? parseInt(maxUsesVal) : null;
  const expiryVal = document.getElementById('edit-m-expiry').value;
  const expiresAt = expiryVal ? new Date(expiryVal).toISOString() : null;

  const oldAction = document.querySelector('input[name="edit-m-old-action"]:checked')?.value || 'keep';
  const deleteOldCodes = oldAction === 'delete';
  const deactivateOldCodes = oldAction === 'deactivate';

  const errDiv = document.getElementById('edit-marketer-error');
  const btn = document.getElementById('btn-submit-edit-marketer');

  if (!name || !phone) {
    errDiv.innerText = 'يرجى إدخال اسم المسوق ورقم الهاتف';
    errDiv.classList.remove('hidden');
    return;
  }

  errDiv.classList.add('hidden');
  btn.disabled = true;
  btn.innerText = 'جاري الحفظ...';

  try {
    const payload = {
      name,
      phone,
      phoneNumber: phone,
      commissionType: commType,
      commissionValue: commValue,
      isActive,
      deleteOldCodes,
      deactivateOldCodes
    };

    if (newCode) {
      payload.discountCode = newCode;
      payload.discountType = discType;
      payload.discountValue = discValue;
      payload.maxUses = maxUses;
      payload.expiresAt = expiresAt;
    }

    const res = await authFetch(`${API_BASE}/Marketers/${marketerId}`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    });
    const data = await res.json();
    btn.disabled = false;
    btn.innerText = 'حفظ التعديلات';

    if (res.ok && data.success) {
      closeEditMarketerModal();
      showToast('تم التعديل بنجاح!', newCode ? `تم حفظ بيانات المسوق وإصدار الكود الجديد "${newCode}".` : 'تم حفظ بيانات المسوق بنجاح.', 'success');
      addLog('SUCCESS', `تعديل بيانات المسوق: ${name}` + (newCode ? ` بكود جديد ${newCode}` : ''), 'MarketerAdmin');
      fetchMarketers();
      fetchDiscountCodes();
    } else {
      errDiv.innerText = data.message || 'فشل تعديل بيانات المسوق';
      errDiv.classList.remove('hidden');
    }
  } catch (e) {
    btn.disabled = false;
    btn.innerText = 'حفظ التعديلات';
    errDiv.innerText = 'حدث خطأ في الاتصال بالخادم';
    errDiv.classList.remove('hidden');
  }
}