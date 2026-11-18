(() => {
    const root = document.documentElement;
    const themeKey = 'unidoc-theme';
    let savedTheme = localStorage.getItem(themeKey);
    if (!savedTheme) {
        savedTheme = localStorage.getItem('deanflow-theme');
        if (savedTheme) localStorage.setItem(themeKey, savedTheme);
    }
    if (savedTheme === 'dark' || savedTheme === 'light') {
        root.dataset.theme = savedTheme;
    } else {
        root.dataset.theme = 'light';
    }

    document.getElementById('themeToggle')?.addEventListener('click', () => {
        root.dataset.theme = root.dataset.theme === 'dark' ? 'light' : 'dark';
        localStorage.setItem(themeKey, root.dataset.theme);
    });

    const sidebar = document.getElementById('sidebar');
    const scrim = document.getElementById('sidebarScrim');
    const appShell = document.getElementById('appShell');
    const sidebarBreakpoint = window.matchMedia('(max-width: 820px)');
    const closeSidebar = () => {
        sidebar?.classList.remove('open');
        scrim?.classList.remove('visible');
        document.getElementById('sidebarToggle')?.setAttribute('aria-expanded', 'false');
    };
    const syncSidebar = () => {
        closeSidebar();
        const collapsed = localStorage.getItem('unidoc-sidebar-collapsed') === 'true';
        appShell?.classList.toggle('is-collapsed', !sidebarBreakpoint.matches && collapsed);
        if (!sidebarBreakpoint.matches) document.getElementById('sidebarToggle')?.setAttribute('aria-expanded', String(!collapsed));
    };
    document.querySelectorAll('.side-link').forEach(link => {
        const label = link.querySelector('span')?.textContent?.trim();
        if (label) {
            link.dataset.tooltip = label;
            link.setAttribute('aria-label', label);
        }
    });
    syncSidebar();
    sidebarBreakpoint.addEventListener('change', syncSidebar);
    document.getElementById('sidebarToggle')?.addEventListener('click', event => {
        if (sidebarBreakpoint.matches) {
            const opened = sidebar?.classList.toggle('open') ?? false;
            scrim?.classList.toggle('visible', opened);
            event.currentTarget.setAttribute('aria-expanded', String(opened));
            return;
        }
        const collapsed = appShell?.classList.toggle('is-collapsed') ?? false;
        localStorage.setItem('unidoc-sidebar-collapsed', String(collapsed));
        event.currentTarget.setAttribute('aria-expanded', String(!collapsed));
    });
    scrim?.addEventListener('click', closeSidebar);
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') closeSidebar();
        if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
            const search = document.querySelector('.global-search input');
            if (search) {
                event.preventDefault();
                search.focus();
            }
        }
    });

    document.querySelectorAll('[data-toggle-password]').forEach(toggle => {
        toggle.addEventListener('click', () => {
            const input = document.getElementById(toggle.dataset.togglePassword);
            if (!input) return;
            const showing = input.type === 'text';
            input.type = showing ? 'password' : 'text';
            toggle.textContent = showing ? 'Көрсету' : 'Жасыру';
            toggle.setAttribute('aria-label', showing ? 'Құпиясөзді көрсету' : 'Құпиясөзді жасыру');
        });
    });

    let pendingForm = null;
    let pendingSubmitter = null;
    const confirmElement = document.getElementById('confirmModal');
    const confirmModal = confirmElement && window.bootstrap
        ? bootstrap.Modal.getOrCreateInstance(confirmElement)
        : null;
    const showToast = (message, kind = '') => {
        const region = document.getElementById('toastRegion');
        if (!region || !message) return;
        const toast = document.createElement('div');
        toast.className = ('app-toast ' + kind).trim();
        toast.setAttribute('role', 'status');
        toast.textContent = message;
        region.append(toast);
        window.setTimeout(() => toast.remove(), 4200);
    };

    document.querySelectorAll('form[data-confirm]').forEach(form => {
        form.addEventListener('submit', event => {
            if (form.matches('.approval-decision-form')) {
                const comment = form.querySelector('textarea[name="comment"]');
                const needsComment = ['return', 'reject'].includes(event.submitter?.value);
                if (comment) {
                    comment.required = needsComment;
                    comment.setCustomValidity(needsComment && !comment.value.trim() ? 'Қайтару немесе қабылдамау себебін жазыңыз.' : '');
                    if (!comment.reportValidity()) {
                        event.preventDefault();
                        comment.focus();
                        return;
                    }
                }
            }
            if (form.dataset.confirmed === 'true') {
                delete form.dataset.confirmed;
                setFormLoading(form, event.submitter);
                return;
            }
            if (!confirmModal || !confirmElement) {
                event.preventDefault();
                showToast('Растау терезесі ашылмады. Бетті жаңартып көріңіз.', 'error');
                return;
            }
            event.preventDefault();
            pendingForm = form;
            pendingSubmitter = event.submitter ?? null;
            const message = document.getElementById('confirmModalMessage');
            const decisionLabel = { approve: 'Келісу', return: 'Қайтару', reject: 'Қабылдамау' }[pendingSubmitter?.value];
            if (message) message.textContent = decisionLabel
                ? `${decisionLabel} шешімін құжатқа тіркеуді растайсыз ба? Әрекет тарихта сақталады.`
                : form.dataset.confirm || 'Осы әрекетті орындауға сенімдісіз бе?';
            confirmModal.show();
        });
    });

    document.getElementById('confirmModalSubmit')?.addEventListener('click', () => {
        if (!pendingForm) return;
        const form = pendingForm;
        const submitter = pendingSubmitter;
        pendingForm = null;
        pendingSubmitter = null;
        form.dataset.confirmed = 'true';
        confirmModal?.hide();
        if (submitter) form.requestSubmit(submitter);
        else form.requestSubmit();
    });

    document.querySelectorAll('form:not([data-confirm])').forEach(form => {
        form.addEventListener('submit', event => setFormLoading(form, event.submitter));
    });

    function setFormLoading(form, submitter = null) {
        const button = submitter || form.querySelector('button[type="submit"],input[type="submit"]');
        if (!button || button.disabled) return;
        button.dataset.originalText = button.tagName === 'INPUT' ? button.value : button.textContent;
        button.disabled = true;
        button.setAttribute('aria-busy', 'true');
        if (button.tagName === 'INPUT') button.value = 'Жіберілуде…';
        else {
            button.classList.add('is-loading');
            button.insertAdjacentHTML('afterbegin', '<span class="submit-spinner" aria-hidden="true"></span>');
        }
    }

    document.querySelectorAll('[data-select-all]').forEach(master => {
        master.addEventListener('change', () => {
            document.querySelectorAll(master.dataset.selectAll).forEach(item => { item.checked = master.checked; });
        });
    });
})();
