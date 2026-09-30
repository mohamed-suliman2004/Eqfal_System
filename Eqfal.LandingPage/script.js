/**
 * Eqfal System - Landing Page Interactive Script
 * High-performance, lightweight vanilla JavaScript
 */

document.addEventListener('DOMContentLoaded', () => {
  initThemeToggle();
  initNavbar();
  initMobileDrawer();
  initCounters();
  initSimulator();
  initFaqAccordion();
  initPhoneInteractive();
  initSupportModal();
  initPricingCycleSwitcher();
  initFeatureFlipper();
});

/* ==========================================================================
   0. Theme Toggle (Light / Dark Mode with Light as Default)
   ========================================================================== */
function initThemeToggle() {
  const toggleBtns = document.querySelectorAll('.theme-toggle-btn');
  const savedTheme = localStorage.getItem('eqfal_theme') || 'light';

  function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    document.body.setAttribute('data-theme', theme);
    localStorage.setItem('eqfal_theme', theme);

    toggleBtns.forEach(btn => {
      const isLight = theme === 'light';
      btn.setAttribute('title', isLight ? 'تبديل إلى الوضع الليلي' : 'تبديل إلى الوضع النهاري');
      btn.setAttribute('aria-label', isLight ? 'تبديل إلى الوضع الليلي' : 'تبديل إلى الوضع النهاري');
    });
  }

  // Apply saved or default light theme
  applyTheme(savedTheme);

  // Toggle handlers
  toggleBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      const current = document.documentElement.getAttribute('data-theme') || 'light';
      const target = current === 'light' ? 'dark' : 'light';
      applyTheme(target);
    });
  });
}

/* ==========================================================================
   1. Navbar Scrolling & Active State
   ========================================================================== */
function initNavbar() {
  const navbar = document.getElementById('navbar');
  const navLinks = document.querySelectorAll('.nav-link');
  const sections = document.querySelectorAll('section[id]');

  window.addEventListener('scroll', () => {
    // Add shadow/dark background when scrolled
    if (window.scrollY > 40) {
      navbar.classList.add('scrolled');
    } else {
      navbar.classList.remove('scrolled');
    }

    // Active Section Tracking
    let current = '';
    sections.forEach((section) => {
      const sectionTop = section.offsetTop - 120;
      const sectionHeight = section.offsetHeight;
      if (window.scrollY >= sectionTop && window.scrollY < sectionTop + sectionHeight) {
        current = section.getAttribute('id');
      }
    });

    navLinks.forEach((link) => {
      link.classList.remove('active');
      if (link.getAttribute('href') === `#${current}`) {
        link.classList.add('active');
      }
    });
  }, { passive: true });
}

/* ==========================================================================
   2. Mobile Drawer
   ========================================================================== */
function initMobileDrawer() {
  const menuToggle = document.getElementById('menuToggle');
  const drawer = document.getElementById('mobileDrawer');
  const drawerClose = document.getElementById('drawerClose');
  const drawerLinks = document.querySelectorAll('.drawer-link');

  if (!menuToggle || !drawer) return;

  function openDrawer() {
    drawer.classList.add('open');
    document.body.style.overflow = 'hidden';
  }

  function closeDrawer() {
    drawer.classList.remove('open');
    document.body.style.overflow = '';
  }

  menuToggle.addEventListener('click', openDrawer);
  if (drawerClose) drawerClose.addEventListener('click', closeDrawer);

  drawerLinks.forEach((link) => {
    link.addEventListener('click', closeDrawer);
  });

  // Close when clicking outside
  document.addEventListener('click', (e) => {
    if (drawer.classList.contains('open') && !drawer.contains(e.target) && !menuToggle.contains(e.target)) {
      closeDrawer();
    }
  });
}

/* ==========================================================================
   3. Animated Stat Counters
   ========================================================================== */
function initCounters() {
  const counters = document.querySelectorAll('.counter');
  if (!counters.length) return;

  const observer = new IntersectionObserver((entries, obs) => {
    entries.forEach((entry) => {
      if (entry.isIntersecting) {
        const counter = entry.target;
        const target = parseFloat(counter.getAttribute('data-target'));
        const isDecimal = target % 1 !== 0;
        let count = 0;
        const speed = target > 50 ? 25 : 50;

        const updateCount = () => {
          const increment = isDecimal ? (target / (1000 / speed)) : Math.ceil(target / (1000 / speed));
          if (count < target) {
            count += increment;
            if (count > target) count = target;
            counter.innerText = isDecimal ? count.toFixed(1) : Math.floor(count);
            setTimeout(updateCount, speed);
          } else {
            counter.innerText = isDecimal ? target.toFixed(1) : target;
          }
        };

        updateCount();
        obs.unobserve(counter);
      }
    });
  }, { threshold: 0.5 });

  counters.forEach((counter) => observer.observe(counter));
}

/* ==========================================================================
   4. Interactive WhatsApp AI Message Simulator
   ========================================================================== */
function initSimulator() {
  const input = document.getElementById('simInputMessage');
  const btn = document.getElementById('btnSimulate');
  const presetBtns = document.querySelectorAll('.sim-preset-btn');
  const simResultBox = document.getElementById('simResultBox');

  const elId = document.getElementById('simVoucherId');
  const elBadge = document.getElementById('simVoucherTypeBadge');
  const elParty = document.getElementById('simParty');
  const elAmount = document.getElementById('simAmount');
  const elCat = document.getElementById('simCategory');
  const elTime = document.getElementById('simProcessTime');

  if (!btn || !input) return;

  // Dynamic textarea height calculation (no unnecessary vertical scrollbar)
  function adjustTextareaHeight() {
    input.style.height = 'auto';
    const newHeight = Math.min(Math.max(input.scrollHeight, 68), 160);
    input.style.height = newHeight + 'px';
  }
  input.addEventListener('input', () => {
    presetBtns.forEach(p => p.classList.remove('active'));
    adjustTextareaHeight();
  });
  setTimeout(adjustTextareaHeight, 100);

  // Preset quick fill buttons
  presetBtns.forEach((preset) => {
    preset.addEventListener('click', () => {
      presetBtns.forEach(p => p.classList.remove('active'));
      preset.classList.add('active');
      const msg = preset.getAttribute('data-msg');
      input.value = msg;
      adjustTextareaHeight();
      parseMessage();
    });
  });

  btn.addEventListener('click', parseMessage);

  function parseMessage() {
    const text = input.value.trim();
    if (!text) {
      // Sleek custom toast notification
      showToast('يرجى كتابة نص رسالة الواتساب أو اختيار أحد النماذج الجاهزة لتجربة التحليل الفوري!', 'warning', 'حقل الرسالة فارغ');
      
      // Interactive shake effect on input box
      input.classList.remove('shake-error');
      void input.offsetWidth; // trigger DOM reflow
      input.classList.add('shake-error');
      input.focus();
      return;
    }

    // Remove any error classes once user has text
    input.classList.remove('shake-error');

    // Visual button loading state
    const originalBtnHtml = btn.innerHTML;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> <span>جاري المعالجة بالذكاء الاصطناعي...</span>';
    btn.disabled = true;

    // Simulate real AI processing latency
    setTimeout(() => {
      btn.innerHTML = originalBtnHtml;
      btn.disabled = false;

      // Rule-based heuristic simulation matching Eqfal's AI extraction
      let amount = '1,500.00 د.ل';
      let party = 'غير محدد';
      let category = 'حوالة عامة';
      let voucherType = 'سند مالي';
      let isIncome = true;

      // Extract amount digits
      const digitsMatch = text.replace(/,/g, '').match(/\d+(\.\d+)?/);
      if (digitsMatch) {
        const num = parseFloat(digitsMatch[0]);
        amount = num.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' د.ل';
      }

      // Check intent
      if (text.includes('استلمت') || text.includes('قبضت') || text.includes('وصلني') || text.includes('دفعة من')) {
        voucherType = 'سند قبض مالي';
        category = 'قبض نقدي / إيداع';
        isIncome = true;
      } else if (text.includes('حولت') || text.includes('دفعت') || text.includes('صرفت') || text.includes('إيجار') || text.includes('سداد')) {
        voucherType = 'سند صرف مالي';
        category = 'تحويل مالي / مصروف';
        isIncome = false;
      }

      // Extract party name
      if (text.includes('السيد سالم') || text.includes('سالم')) {
        party = 'السيد سالم';
      } else if (text.includes('شركة النور') || text.includes('النور')) {
        party = 'شركة النور للحلول';
      } else if (text.includes('الأفق') || text.includes('شركة الأفق')) {
        party = 'شركة الأفق التجارية';
      } else if (text.includes('الحاج ناصر') || text.includes('ناصر')) {
        party = 'الحاج ناصر';
      } else if (text.includes('فتحي')) {
        party = 'فتحي عبد الله';
      } else if (text.includes('أحمد')) {
        party = 'أحمد محمد';
      } else {
        // Fallback search after "من" or "لـ"
        const fromMatch = text.match(/(من|لحساب|لشركة|للسيد)\s+([\u0621-\u064A\s]{3,20})/);
        if (fromMatch && fromMatch[2]) {
          party = fromMatch[2].trim();
        } else {
          party = 'عميل نقدي / مورد';
        }
      }

      // Update UI
      const randomId = Math.floor(10000 + Math.random() * 90000);
      elId.innerText = `#EQ-${randomId}`;
      elBadge.innerText = voucherType;
      elParty.innerText = party;
      elAmount.innerText = (isIncome ? '+' : '-') + ' ' + amount;
      elAmount.className = isIncome ? 'v-val text-green' : 'v-val text-blue';
      elCat.innerText = category;
      
      const calcSpeed = (0.18 + Math.random() * 0.12).toFixed(2);
      elTime.innerText = `استغرق: ${calcSpeed} ثانية`;

      // Update Double Entry Ledger
      const doubleEntryDiv = simResultBox.querySelector('.double-entry-preview');
      if (doubleEntryDiv) {
        if (isIncome) {
          doubleEntryDiv.innerHTML = `
            <div class="entry-row">
              <span class="acc-name"><i class="fa-solid fa-vault"></i> حساب الخزينة / الصندوق</span>
              <span class="acc-side debit">مدين (+) ${amount}</span>
            </div>
            <div class="entry-row">
              <span class="acc-name"><i class="fa-solid fa-user-tag"></i> حساب العملاء (${party})</span>
              <span class="acc-side credit">دائن (-) ${amount}</span>
            </div>
          `;
        } else {
          doubleEntryDiv.innerHTML = `
            <div class="entry-row">
              <span class="acc-name"><i class="fa-solid fa-user-tag"></i> حساب الموردين (${party})</span>
              <span class="acc-side debit">مدين (+) ${amount}</span>
            </div>
            <div class="entry-row">
              <span class="acc-name"><i class="fa-solid fa-building-columns"></i> حساب المصرف / الصندوق</span>
              <span class="acc-side credit">دائن (-) ${amount}</span>
            </div>
          `;
        }
      }

      // Highlight animation
      simResultBox.style.boxShadow = '0 0 25px rgba(16, 185, 129, 0.4)';
      setTimeout(() => {
        simResultBox.style.boxShadow = '';
      }, 1000);

      // On mobile screens, scroll smooth so user clearly sees the parsed voucher result
      if (window.innerWidth < 768 && simResultBox) {
        simResultBox.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      }

    }, 380);
  }
}

/* ==========================================================================
   5. FAQ Accordion
   ========================================================================== */
function initFaqAccordion() {
  const faqItems = document.querySelectorAll('.faq-item');

  faqItems.forEach((item) => {
    const questionBtn = item.querySelector('.faq-question');
    if (!questionBtn) return;

    questionBtn.addEventListener('click', () => {
      const isActive = item.classList.contains('active');

      // Close all other items
      faqItems.forEach((other) => other.classList.remove('active'));

      // Toggle current item
      if (!isActive) {
        item.classList.add('active');
      }
    });
  });
}

/* ==========================================================================
   6. Sleek Luxury Toast Notification System
   ========================================================================== */
function showToast(message, type = 'warning', title = 'تنبيه') {
  let container = document.getElementById('toastContainer');
  if (!container) {
    container = document.createElement('div');
    container.id = 'toastContainer';
    container.className = 'toast-container';
    document.body.appendChild(container);
  }

  // Remove existing toast if any
  container.innerHTML = '';

  const toast = document.createElement('div');
  toast.className = `toast-card toast-${type}`;
  
  const iconClass = type === 'warning' 
    ? 'fa-solid fa-triangle-exclamation' 
    : (type === 'error' ? 'fa-solid fa-circle-xmark' : 'fa-solid fa-circle-check');

  toast.innerHTML = `
    <div class="toast-icon-wrap">
      <i class="${iconClass}"></i>
    </div>
    <div class="toast-content">
      <div class="toast-title">${title}</div>
      <div class="toast-msg">${message}</div>
    </div>
    <button class="toast-close" type="button" aria-label="إغلاق">&times;</button>
    <div class="toast-progress"></div>
  `;

  container.appendChild(toast);

  // Trigger entrance transition
  requestAnimationFrame(() => {
    toast.classList.add('show');
  });

  const closeBtn = toast.querySelector('.toast-close');
  closeBtn.addEventListener('click', () => {
    dismissToast(toast);
  });

  // Auto dismiss after 4.2 seconds
  const autoTimer = setTimeout(() => {
    dismissToast(toast);
  }, 4200);

  function dismissToast(el) {
    clearTimeout(autoTimer);
    el.classList.remove('show');
    el.classList.add('hide');
    setTimeout(() => {
      el.remove();
    }, 350);
  }
}

/* ==========================================================================
   7. Interactive Phone Mockup Tabs & Controls
   ========================================================================== */
function initPhoneInteractive() {
  const phoneTabs = document.querySelectorAll('[data-phone-tab]');
  const phoneViews = {
    dashboard: document.getElementById('phoneViewDashboard'),
    ops: document.getElementById('phoneViewOps'),
    settings: document.getElementById('phoneViewSettings'),
    analyze: document.getElementById('phoneViewAnalyze')
  };

  function switchPhoneTab(tabName, extraParam) {
    phoneTabs.forEach(t => {
      if (t.getAttribute('data-phone-tab') === tabName) {
        t.classList.add('active');
      } else {
        t.classList.remove('active');
      }
    });

    Object.keys(phoneViews).forEach(key => {
      const view = phoneViews[key];
      if (!view) return;
      if (key === tabName) {
        view.classList.add('active');
      } else {
        view.classList.remove('active');
      }
    });

    // If navigating to ops with category
    if (tabName === 'ops' && extraParam) {
      const opsTitle = document.getElementById('phoneOpsTitle');
      if (opsTitle) {
        if (extraParam === 'تسليم') {
          opsTitle.innerHTML = '<span>تسليم (46)</span> <i class="fa-solid fa-arrow-up title-dir-icon"></i>';
        } else {
          opsTitle.innerHTML = '<span>استلام (97)</span> <i class="fa-solid fa-arrow-down title-dir-icon"></i>';
        }
      }
    }
  }

  // Bind bottom nav tabs
  phoneTabs.forEach(tab => {
    tab.addEventListener('click', () => {
      const tabName = tab.getAttribute('data-phone-tab');
      switchPhoneTab(tabName);
    });
  });

  // Bind all data-nav-target elements (cards, back buttons, analyze button, appbar user icon)
  document.querySelectorAll('[data-nav-target]').forEach(el => {
    el.addEventListener('click', (e) => {
      e.stopPropagation();
      const target = el.getAttribute('data-nav-target');
      const cat = el.getAttribute('data-cat');
      switchPhoneTab(target, cat);
    });
  });

  // Real Flutter Segmented Tabs in Operations view
  const segTabs = document.querySelectorAll('.real-seg-tab');
  const feedCards = document.querySelectorAll('#phoneOpsFeed > div');
  const opsTitle = document.getElementById('phoneOpsTitle');

  segTabs.forEach(segTab => {
    segTab.addEventListener('click', () => {
      segTabs.forEach(t => t.classList.remove('active'));
      segTab.classList.add('active');

      const segType = segTab.getAttribute('data-seg');

      feedCards.forEach(card => {
        const cardType = card.getAttribute('data-card-type');
        if (segType === 'all' || cardType === segType) {
          card.style.display = 'flex';
        } else {
          card.style.display = 'none';
        }
      });

      if (opsTitle) {
        if (segType === 'all') opsTitle.innerHTML = '<span>الكل (145)</span> <i class="fa-solid fa-layer-group title-dir-icon"></i>';
        else if (segType === 'unreviewed') opsTitle.innerHTML = '<span>غير مراجعة (51)</span> <i class="fa-solid fa-arrow-down title-dir-icon"></i>';
        else if (segType === 'reviewed') opsTitle.innerHTML = '<span>مراجعة (46)</span> <i class="fa-solid fa-circle-check title-dir-icon text-green"></i>';
        else if (segType === 'deleted') opsTitle.innerHTML = '<span>محذوفة (2)</span> <i class="fa-solid fa-trash-can title-dir-icon text-danger"></i>';
      }
    });
  });

  // Biometric toggle switch in Settings
  const switchEl = document.querySelector('.real-flutter-switch');
  if (switchEl) {
    switchEl.addEventListener('click', () => {
      switchEl.classList.toggle('on');
    });
  }

  // Open support modal from phone settings support link
  const supportLink = document.getElementById('btnSettingsSupportLink');
  if (supportLink) {
    supportLink.addEventListener('click', () => {
      const modal = document.getElementById('supportModal');
      if (modal) {
        modal.classList.add('open');
        modal.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden';
      }
    });
  }

  // Live clock
  const clockEl = document.getElementById('phoneLiveClock');
  if (clockEl) {
    const updateTime = () => {
      const now = new Date();
      let hours = now.getHours();
      const minutes = now.getMinutes().toString().padStart(2, '0');
      const isPm = hours >= 12;
      hours = hours % 12 || 12;
      clockEl.textContent = `${hours}:${minutes} ${isPm ? 'م' : 'ص'}`;
    };
    updateTime();
    setInterval(updateTime, 30000);
  }

  // Dismiss floating live badge and hint on ANY tap/click inside phone screen or badge
  const dismissLiveBadge = () => {
    const liveBadge = document.getElementById('phoneFloatingLiveBadge') || document.querySelector('.phone-floating-live-badge');
    if (liveBadge && !liveBadge.classList.contains('dismissed')) {
      liveBadge.classList.add('dismissed');
      setTimeout(() => {
        liveBadge.style.display = 'none';
      }, 350);
    }
  };

  // Listen on all phone mockup layers for any click or touch
  const phoneTargets = document.querySelectorAll('.phone-screen, .phone-frame, .phone-mockup-wrapper, .phone-views-wrapper, #phoneFloatingLiveBadge');
  phoneTargets.forEach(target => {
    target.addEventListener('click', dismissLiveBadge, { capture: true });
    target.addEventListener('touchstart', dismissLiveBadge, { capture: true, passive: true });
  });

  // Clicking directly on floating badge simply dismisses it
  const liveBadgeEl = document.getElementById('phoneFloatingLiveBadge');
  if (liveBadgeEl) {
    liveBadgeEl.addEventListener('click', (e) => {
      dismissLiveBadge();
    });
  }
}

/* ==========================================================================
   8. Technical Support Ticket Modal (Exact App Alignment)
   ========================================================================== */
function initSupportModal() {
  const modal = document.getElementById('supportModal');
  const btnOpen = document.getElementById('btnOpenSupportModal');
  const btnOpenDrawer = document.getElementById('btnOpenSupportDrawer');
  const btnClose = document.getElementById('btnCloseSupportModal');
  const btnDone = document.getElementById('btnDoneSupportModal');
  const form = document.getElementById('supportTicketForm');
  const formView = document.getElementById('supportFormView');
  const successView = document.getElementById('supportSuccessView');
  const ticketIdEl = document.getElementById('assignedTicketId');
  const btnSubmit = document.getElementById('btnSubmitTicket');

  if (!modal) return;

  function openModal() {
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    document.body.style.overflow = 'hidden';

    // Reset view if previously submitted
    if (formView) formView.style.display = 'block';
    if (successView) successView.style.display = 'none';

    // Focus first input
    const firstInput = document.getElementById('ticketFullName');
    if (firstInput) setTimeout(() => firstInput.focus(), 200);
  }

  function closeModal() {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    document.body.style.overflow = '';
  }

  const btnOpenNav = document.getElementById('btnOpenSupportNav');
  if (btnOpenNav) btnOpenNav.addEventListener('click', openModal);
  if (btnOpen) btnOpen.addEventListener('click', openModal);
  if (btnOpenDrawer) btnOpenDrawer.addEventListener('click', () => {
    // Close drawer if open
    const drawer = document.getElementById('mobileDrawer');
    if (drawer) drawer.classList.remove('open');
    openModal();
  });
  if (btnClose) btnClose.addEventListener('click', closeModal);
  if (btnDone) btnDone.addEventListener('click', closeModal);

  // Close on backdrop click
  modal.addEventListener('click', (e) => {
    if (e.target === modal) {
      closeModal();
    }
  });

  // Close on Escape key
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && modal.classList.contains('open')) {
      closeModal();
    }
  });

  // Handle Form Submit
  if (form) {
    form.addEventListener('submit', async (e) => {
      e.preventDefault();

      const fullName = document.getElementById('ticketFullName').value.trim();
      const phoneNumber = document.getElementById('ticketPhone').value.trim();
      const subject = document.getElementById('ticketSubject').value;
      const email = document.getElementById('ticketEmail').value.trim();
      const message = document.getElementById('ticketMessage').value.trim();

      if (!fullName) {
        showToast('يرجى كتابة الاسم بالكامل.', 'warning', 'حقل مطلوب');
        document.getElementById('ticketFullName').focus();
        return;
      }

      if (!phoneNumber) {
        showToast('يرجى إدخال رقم الهاتف للتواصل.', 'warning', 'حقل مطلوب');
        document.getElementById('ticketPhone').focus();
        return;
      }

      if (!message || message.length < 5) {
        showToast('يرجى توضيح المشكلة أو الطلب بالتفصيل.', 'warning', 'حقل مطلوب');
        document.getElementById('ticketMessage').focus();
        return;
      }

      // Submit loading state
      const originalBtnHtml = btnSubmit.innerHTML;
      btnSubmit.classList.add('loading');
      btnSubmit.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> <span>جارٍ إرسال التذكرة...</span>';

      let assignedId = Math.floor(1020 + Math.random() * 8900);

      try {
        const response = await fetch('https://eqfall.mostanad.ly/api/Support/ticket', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json'
          },
          body: JSON.stringify({
            fullName: fullName,
            phoneNumber: phoneNumber,
            email: email || null,
            subject: subject,
            message: message
          })
        });

        if (response.ok) {
          const data = await response.json();
          if (data && data.ticketId) {
            assignedId = data.ticketId;
          }
        }
      } catch (err) {
        console.warn('Direct API submission note, generated local ticket ID:', err);
      } finally {
        btnSubmit.classList.remove('loading');
        btnSubmit.innerHTML = originalBtnHtml;

        // Show Success View
        if (ticketIdEl) ticketIdEl.textContent = `#${assignedId}`;

        formView.style.display = 'none';
        successView.style.display = 'block';

        showToast(`تم استلام طلبك بنجاح! رقم التذكرة الخاص بك هو #${assignedId}`, 'success', 'تم الإرسال بنجاح');
      }
    });
  }
}

/* ==========================================================================
   8. Pricing Cycle Switcher (Monthly / Yearly Switch Toggle - Zemam Style)
   ========================================================================== */
function initPricingCycleSwitcher() {
  const checkbox = document.getElementById('pricingCycleCheckbox');
  const labelMonthly = document.getElementById('labelMonthly');
  const labelYearly = document.getElementById('labelYearly');
  const discountBadge = document.getElementById('saveDiscountBadge');
  const proPriceEl = document.getElementById('proPlanPrice');
  const proPeriodEl = document.getElementById('proPlanPeriod');
  const cycleNoteEl = document.getElementById('cycleBillingNote');
  const savingBanner = document.getElementById('proSavingBanner');
  const savingText = document.getElementById('proSavingText');
  const actionText = document.getElementById('proActionText');

  if (!proPriceEl) return;

  const updatePricing = (isYearly) => {
    if (checkbox) checkbox.checked = isYearly;

    if (isYearly) {
      if (labelMonthly) labelMonthly.classList.remove('active');
      if (labelYearly) labelYearly.classList.add('active');
    } else {
      if (labelMonthly) labelMonthly.classList.add('active');
      if (labelYearly) labelYearly.classList.remove('active');
    }

    // Animate price change
    proPriceEl.style.opacity = '0';
    proPriceEl.style.transform = 'translateY(-10px)';

    setTimeout(() => {
      if (isYearly) {
        proPriceEl.textContent = '420';
        if (proPeriodEl) proPeriodEl.textContent = 'لكل سنة كاملة (35 د.ل / شهر فقط)';
        if (cycleNoteEl) cycleNoteEl.textContent = 'فاتورة سنوية تدفع كل 365 يوماً — الخيار الأوفر والأعلى قيمة';
        if (actionText) actionText.textContent = 'اشترك سنوياً ووفر 30%';
        if (savingBanner) savingBanner.style.display = 'inline-flex';
        if (savingText) savingText.textContent = 'وفرت 180 د.ل سنوياً (خصم 30% — الأوفر والأعلى قيمة 🔥)';
      } else {
        proPriceEl.textContent = '50';
        if (proPeriodEl) proPeriodEl.textContent = 'لكل شهر واحد (50 د.ل / شهر)';
        if (cycleNoteEl) cycleNoteEl.textContent = 'فاتورة شهرية مرنة تتجدد كل 30 يوماً — بدون أي التزام طويل الأجل';
        if (actionText) actionText.textContent = 'اشترك شهرياً في إقفال برو';
        if (savingBanner) savingBanner.style.display = 'none';
      }

      proPriceEl.style.opacity = '1';
      proPriceEl.style.transform = 'translateY(0)';
    }, 150);
  };

  if (checkbox) {
    checkbox.addEventListener('change', () => {
      updatePricing(checkbox.checked);
    });
  }

  if (labelMonthly) {
    labelMonthly.addEventListener('click', () => updatePricing(false));
  }

  if (labelYearly) {
    labelYearly.addEventListener('click', () => updatePricing(true));
  }

  if (discountBadge) {
    discountBadge.addEventListener('click', () => updatePricing(true));
  }

  // Smooth transition for price element
  proPriceEl.style.transition = 'all 0.2s cubic-bezier(0.4, 0, 0.2, 1)';
}

/* ==========================================================================
   9. Interactive Feature Flipper Component (الكارت التفاعلي الذكي)
   ========================================================================== */
function initFeatureFlipper() {
  const tabs = document.querySelectorAll('.flipper-tab-btn');
  const slides = document.querySelectorAll('.flipper-slide');
  const dots = document.querySelectorAll('.flipper-dot');
  const barFill = document.querySelector('.flipper-timer-bar-fill');
  const btnPrev = document.getElementById('flipperBtnPrev');
  const btnNext = document.getElementById('flipperBtnNext');
  const btnPlayPause = document.getElementById('flipperBtnTogglePlay');
  const counterText = document.getElementById('flipperCounterText');
  const mainCard = document.querySelector('.flipper-main-card');

  if (!slides.length) return;

  let currentIndex = 0;
  const totalSlides = slides.length;
  let isPlaying = true;
  let isHovered = false;
  let progress = 0;
  const slideDuration = 5500; // 5.5 seconds per slide
  const tickInterval = 50; // update bar every 50ms
  let timerInterval = null;

  function goToSlide(index) {
    if (index < 0) index = totalSlides - 1;
    if (index >= totalSlides) index = 0;
    currentIndex = index;

    // Update slides
    slides.forEach((slide, i) => {
      slide.classList.toggle('active', i === currentIndex);
    });

    // Update tabs
    tabs.forEach((tab, i) => {
      tab.classList.toggle('active', i === currentIndex);
    });

    // Update dots
    dots.forEach((dot, i) => {
      dot.classList.toggle('active', i === currentIndex);
    });

    // Update counter
    if (counterText) {
      counterText.textContent = `0${currentIndex + 1} / 0${totalSlides}`;
    }

    // Reset progress
    progress = 0;
    if (barFill) barFill.style.width = '0%';
  }

  function startTimer() {
    stopTimer();
    timerInterval = setInterval(() => {
      if (!isPlaying || isHovered) return;
      progress += (tickInterval / slideDuration) * 100;
      if (barFill) barFill.style.width = `${Math.min(progress, 100)}%`;

      if (progress >= 100) {
        goToSlide(currentIndex + 1);
      }
    }, tickInterval);
  }

  function stopTimer() {
    if (timerInterval) {
      clearInterval(timerInterval);
      timerInterval = null;
    }
  }

  // Click on tabs
  tabs.forEach((tab) => {
    tab.addEventListener('click', () => {
      const targetIndex = parseInt(tab.getAttribute('data-slide-index') || '0', 10);
      goToSlide(targetIndex);
    });
  });

  // Click on dots
  dots.forEach((dot) => {
    dot.addEventListener('click', () => {
      const targetIndex = parseInt(dot.getAttribute('data-slide-index') || '0', 10);
      goToSlide(targetIndex);
    });
  });

  // Next / Prev buttons
  if (btnNext) {
    btnNext.addEventListener('click', () => {
      goToSlide(currentIndex + 1);
    });
  }

  if (btnPrev) {
    btnPrev.addEventListener('click', () => {
      goToSlide(currentIndex - 1);
    });
  }

  // Play / Pause toggle
  if (btnPlayPause) {
    btnPlayPause.addEventListener('click', () => {
      isPlaying = !isPlaying;
      btnPlayPause.innerHTML = isPlaying ? '<i class="fa-solid fa-pause"></i>' : '<i class="fa-solid fa-play"></i>';
      btnPlayPause.setAttribute('title', isPlaying ? 'إيقاف التبديل التلقائي مؤقتاً' : 'تشغيل التبديل التلقائي');
    });
  }

  // Pause on hover
  if (mainCard) {
    mainCard.addEventListener('mouseenter', () => {
      isHovered = true;
    });
    mainCard.addEventListener('mouseleave', () => {
      isHovered = false;
    });
  }

  // Initialize first slide and start timer
  goToSlide(0);
  startTimer();
}
