// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

window.togglePasswordVisibility = function (inputId, button) {
    const input = document.getElementById(inputId);
    const icon = button && button.querySelector('i');
    if (!input || !icon) return;

    const showPassword = input.type === 'password';
    input.type = showPassword ? 'text' : 'password';
    icon.classList.toggle('bi-eye', !showPassword);
    icon.classList.toggle('bi-eye-slash', showPassword);
    button.setAttribute('aria-label', showPassword ? 'Hide password' : 'Show password');
    button.setAttribute('aria-pressed', String(showPassword));
};

window.invoiceCurrency = {
    format(value) {
        return '$' + value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    },
    parse(value) {
        return parseFloat(value.replace(/[$,]/g, '')) || 0;
    }
};

document.addEventListener('DOMContentLoaded', function () {
    const sidebar = document.getElementById('appSidebar');
    const toggle = document.getElementById('sidebarRevealToggle');
    const closeToggle = document.getElementById('sidebarCloseToggle');
    const backdrop = document.getElementById('sidebarBackdrop');
    if (!sidebar || !toggle) return;

    const canHover = window.matchMedia('(hover: hover) and (pointer: fine)').matches;
    let closeTimer;
    let isPinned = false;
    let skipFocusOpen = false;

    function setOpen(isOpen) {
        sidebar.classList.toggle('is-open', isOpen);
        sidebar.setAttribute('aria-hidden', String(!isOpen));
        document.body.classList.toggle('sidebar-open', isOpen);
        toggle.classList.toggle('is-pinned', isOpen && isPinned);
        toggle.setAttribute('aria-expanded', String(isOpen));
        toggle.setAttribute('aria-label', isOpen ? 'Close navigation' : 'Open navigation');
        if (backdrop) backdrop.classList.toggle('is-visible', isOpen);
    }

    function cancelClose() {
        window.clearTimeout(closeTimer);
    }

    function scheduleClose() {
        cancelClose();
        if (!isPinned && !sidebar.contains(document.activeElement) &&
            document.activeElement !== toggle) {
            closeTimer = window.setTimeout(function () {
                setOpen(false);
            }, 180);
        }
    }

    toggle.addEventListener('click', function () {
        cancelClose();
        isPinned = !isPinned;
        setOpen(isPinned);
    });

    if (closeToggle) {
        closeToggle.addEventListener('click', function () {
            cancelClose();
            isPinned = false;
            setOpen(false);
            skipFocusOpen = true;
            toggle.focus();
        });
    }

    if (canHover) {
        toggle.addEventListener('mouseenter', function () {
            cancelClose();
            setOpen(true);
        });
        toggle.addEventListener('mouseleave', scheduleClose);
        sidebar.addEventListener('mouseenter', cancelClose);
        sidebar.addEventListener('mouseleave', scheduleClose);
    }

    toggle.addEventListener('focus', function () {
        if (skipFocusOpen) {
            skipFocusOpen = false;
            return;
        }
        cancelClose();
        setOpen(true);
    });
    toggle.addEventListener('blur', function (event) {
        if (!sidebar.contains(event.relatedTarget)) scheduleClose();
    });
    sidebar.addEventListener('focusin', cancelClose);
    sidebar.addEventListener('focusout', function (event) {
        if (!sidebar.contains(event.relatedTarget) && event.relatedTarget !== toggle) scheduleClose();
    });

    if (backdrop) {
        backdrop.addEventListener('click', function () {
            isPinned = false;
            setOpen(false);
        });
    }

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' && sidebar.classList.contains('is-open')) {
            isPinned = false;
            setOpen(false);
            if (document.activeElement !== toggle) {
                skipFocusOpen = true;
                toggle.focus();
            }
        }
    });
});
