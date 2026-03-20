/* site.js  La Colonial Fabrica de Hilos S.A. */
'use strict';

// ===== NAVBAR: scroll effect + hamburguesa =====
(function () {
    const navbar  = document.getElementById('navbar');
    const toggle  = document.getElementById('navToggle');
    const menu    = document.getElementById('navMenu');
    if (!navbar || !toggle || !menu) return;

    // Scroll -> clase scrolled
    const onScroll = () => {
        navbar.classList.toggle('scrolled', window.scrollY > 60);
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();

    // Hamburguesa
    toggle.addEventListener('click', () => {
        const isOpen = menu.classList.toggle('open');
        toggle.classList.toggle('open', isOpen);
        toggle.setAttribute('aria-expanded', isOpen);
        document.body.style.overflow = isOpen ? 'hidden' : '';
    });

    // Cerrar al hacer click en un enlace
    menu.querySelectorAll('a').forEach(a => {
        a.addEventListener('click', () => {
            menu.classList.remove('open');
            toggle.classList.remove('open');
            toggle.setAttribute('aria-expanded', 'false');
            document.body.style.overflow = '';
        });
    });

    // Cerrar al hacer click fuera del menú
    document.addEventListener('click', (e) => {
        if (!navbar.contains(e.target)) {
            menu.classList.remove('open');
            toggle.classList.remove('open');
            toggle.setAttribute('aria-expanded', 'false');
            document.body.style.overflow = '';
        }
    });
})();

// ===== SCROLL TO TOP =====
(function () {
    const btn = document.getElementById('scrollTop');
    if (!btn) return;
    const onScroll = () => btn.classList.toggle('visible', window.scrollY > 400);
    window.addEventListener('scroll', onScroll, { passive: true });
    btn.addEventListener('click', () => window.scrollTo({ top: 0, behavior: 'smooth' }));
})();

// ===== LIGHTBOX =====
(function () {
    const overlay = document.getElementById('lightbox');
    const img     = document.getElementById('lightboxImg');
    const btnClose= document.getElementById('lightboxClose');
    if (!overlay || !img) return;

    const open = (src, alt) => {
        img.src = src;
        img.alt = alt || '';
        overlay.classList.add('open');
        document.body.style.overflow = 'hidden';
        btnClose.focus();
    };
    const close = () => {
        overlay.classList.remove('open');
        document.body.style.overflow = '';
        img.src = '';
    };

    // Delegación de eventos: cualquier elemento con data-lightbox
    document.addEventListener('click', (e) => {
        const el = e.target.closest('[data-lightbox]');
        if (el) { e.preventDefault(); open(el.dataset.lightbox, el.dataset.alt); }
    });

    btnClose && btnClose.addEventListener('click', close);
    overlay.addEventListener('click', (e) => { if (e.target === overlay) close(); });
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') close(); });
})();

// ===== AOS (Animate on Scroll) =====
if (typeof AOS !== 'undefined') {
    AOS.init({
        duration: 600,
        once: true,
        offset: 60,
        easing: 'ease-out-cubic'
    });
}

// ===== COUNTER animado para stats =====
(function () {
    const counters = document.querySelectorAll('[data-counter]');
    if (!counters.length) return;

    const animate = (el) => {
        const target = parseInt(el.dataset.counter, 10);
        const suffix = el.dataset.suffix || '';
        const duration = 1800;
        const step = 16;
        const increment = target / (duration / step);
        let current = 0;
        const timer = setInterval(() => {
            current += increment;
            if (current >= target) { current = target; clearInterval(timer); }
            el.textContent = Math.floor(current).toLocaleString('es') + suffix;
        }, step);
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(e => { if (e.isIntersecting) { animate(e.target); observer.unobserve(e.target); } });
    }, { threshold: 0.4 });

    counters.forEach(c => observer.observe(c));
})();

// ===== Formulario de contacto: validación cliente =====
(function () {
    const form = document.getElementById('contactoForm');
    if (!form) return;

    form.addEventListener('submit', (e) => {
        let valid = true;
        form.querySelectorAll('[required]').forEach(field => {
            field.classList.remove('field-error');
            if (!field.value.trim()) { field.classList.add('field-error'); valid = false; }
        });
        const emailField = form.querySelector('[type="email"]');
        if (emailField && emailField.value && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(emailField.value)) {
            emailField.classList.add('field-error');
            valid = false;
        }
        if (!valid) { e.preventDefault(); form.querySelector('.field-error')?.focus(); }
    });
})();
