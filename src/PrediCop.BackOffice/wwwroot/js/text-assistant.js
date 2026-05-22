(function () {
    'use strict';

    var CORRECT_URL = '/text-assistant/correct';
    var IMPROVE_URL = '/text-assistant/improve';

    var activeTextarea = null;
    var pendingSuggestions = [];

    // ── Init ──────────────────────────────────────────────────────────────────

    function init() {
        document.querySelectorAll('textarea[data-text-assistant]').forEach(attachToolbar);
        initModal();
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    function attachToolbar(ta) {
        var wrap = document.createElement('div');
        wrap.className = 'ta-toolbar d-flex gap-2 mt-1 flex-wrap';

        var btnCorrect = btn('btn-outline-secondary', '<i class="fa-solid fa-spell-check me-1"></i>Corriger l\'orthographe');
        var btnImprove = btn('btn-outline-primary',   '<i class="fa-solid fa-wand-magic-sparkles me-1"></i>Améliorer le texte');

        var status = document.createElement('span');
        status.className = 'ta-status small align-self-center d-none';

        btnCorrect.addEventListener('click', function () { onCorrect(ta, btnCorrect, btnImprove, status); });
        btnImprove.addEventListener('click', function () { onImprove(ta, btnCorrect, btnImprove, status); });

        wrap.appendChild(btnCorrect);
        wrap.appendChild(btnImprove);
        wrap.appendChild(status);
        ta.insertAdjacentElement('afterend', wrap);
    }

    function btn(cls, html) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'btn btn-sm ' + cls;
        b.innerHTML = html;
        return b;
    }

    // ── Spell correction ──────────────────────────────────────────────────────

    function onCorrect(ta, b1, b2, status) {
        var text = ta.value.trim();
        if (!text) { flash(status, 'Le champ est vide.', 'warning'); return; }

        busy(true, b1, b2, status, 'Correction en cours…');

        post(CORRECT_URL, { text: text })
            .then(function (d) {
                if (d.error) { flash(status, d.error, 'danger'); return; }
                if (d.correctedText === text) {
                    flash(status, 'Aucune faute détectée.', 'success');
                } else {
                    ta.value = d.correctedText;
                    ta.dispatchEvent(new Event('input', { bubbles: true }));
                    flash(status, 'Texte corrigé.', 'success');
                }
            })
            .catch(function () { flash(status, 'Erreur réseau.', 'danger'); })
            .finally(function () { busy(false, b1, b2, status); });
    }

    // ── Improvement ───────────────────────────────────────────────────────────

    function onImprove(ta, b1, b2, status) {
        var text = ta.value.trim();
        if (!text) { flash(status, 'Le champ est vide.', 'warning'); return; }

        busy(true, b1, b2, status, 'Analyse en cours…');
        activeTextarea = ta;

        post(IMPROVE_URL, { text: text })
            .then(function (d) {
                if (d.error) { flash(status, d.error, 'danger'); return; }
                if (!d.suggestions || d.suggestions.length === 0) {
                    flash(status, 'Aucune amélioration à suggérer.', 'success');
                    return;
                }
                pendingSuggestions = d.suggestions.map(function (s) {
                    return { original: s.original, suggested: s.suggested, reason: s.reason, accepted: true };
                });
                openModal();
            })
            .catch(function () { flash(status, 'Erreur réseau.', 'danger'); })
            .finally(function () { busy(false, b1, b2, status); });
    }

    // ── Modal ─────────────────────────────────────────────────────────────────

    function initModal() {
        on('ta-apply-btn',   'click', applyAccepted);
        on('ta-accept-all',  'click', function () { setAll(true); });
        on('ta-ignore-all',  'click', function () { setAll(false); });
    }

    function openModal() {
        var list = document.getElementById('ta-suggestions-list');
        var countEl = document.getElementById('ta-suggestions-count');
        if (!list) return;

        list.innerHTML = '';
        if (countEl) countEl.textContent = pendingSuggestions.length;

        pendingSuggestions.forEach(function (s, i) {
            var card = document.createElement('div');
            card.className = 'card mb-2 ta-suggestion-card border-success';
            card.dataset.index = i;

            card.innerHTML =
                '<div class="card-body py-2 px-3">' +
                  '<div class="d-flex justify-content-between align-items-start gap-2 mb-2">' +
                    '<span class="badge bg-secondary small">' + esc(s.reason) + '</span>' +
                    '<button type="button" class="btn btn-xs btn-outline-danger ta-toggle-btn flex-shrink-0" data-index="' + i + '" style="font-size:.75rem;padding:.15rem .4rem">' +
                      '<i class="fa-solid fa-xmark me-1"></i>Ignorer' +
                    '</button>' +
                  '</div>' +
                  '<div class="row g-2">' +
                    '<div class="col-6">' +
                      '<div class="fw-semibold text-danger mb-1" style="font-size:.75rem"><i class="fa-solid fa-circle-xmark me-1"></i>Avant</div>' +
                      '<div class="p-2 rounded bg-danger bg-opacity-10" style="font-size:.8rem;word-break:break-word">' + esc(s.original) + '</div>' +
                    '</div>' +
                    '<div class="col-6">' +
                      '<div class="fw-semibold text-success mb-1" style="font-size:.75rem"><i class="fa-solid fa-circle-check me-1"></i>Après</div>' +
                      '<div class="p-2 rounded bg-success bg-opacity-10" style="font-size:.8rem;word-break:break-word">' + esc(s.suggested) + '</div>' +
                    '</div>' +
                  '</div>' +
                '</div>';

            card.querySelector('.ta-toggle-btn').addEventListener('click', function () { toggle(i); });
            list.appendChild(card);
        });

        updateApplyBtn();

        var modal = new bootstrap.Modal(document.getElementById('ta-improve-modal'));
        modal.show();
    }

    function toggle(i) {
        pendingSuggestions[i].accepted = !pendingSuggestions[i].accepted;
        var card = document.querySelector('.ta-suggestion-card[data-index="' + i + '"]');
        var b = card.querySelector('.ta-toggle-btn');
        if (pendingSuggestions[i].accepted) {
            card.classList.replace('border-secondary', 'border-success');
            card.classList.remove('opacity-50');
            b.className = 'btn btn-xs btn-outline-danger ta-toggle-btn flex-shrink-0';
            b.style.cssText = 'font-size:.75rem;padding:.15rem .4rem';
            b.innerHTML = '<i class="fa-solid fa-xmark me-1"></i>Ignorer';
        } else {
            card.classList.replace('border-success', 'border-secondary');
            card.classList.add('opacity-50');
            b.className = 'btn btn-xs btn-outline-success ta-toggle-btn flex-shrink-0';
            b.style.cssText = 'font-size:.75rem;padding:.15rem .4rem';
            b.innerHTML = '<i class="fa-solid fa-check me-1"></i>Réactiver';
        }
        updateApplyBtn();
    }

    function setAll(accepted) {
        pendingSuggestions.forEach(function (s, i) {
            if (s.accepted !== accepted) toggle(i);
        });
    }

    function updateApplyBtn() {
        var b = document.getElementById('ta-apply-btn');
        if (!b) return;
        var n = pendingSuggestions.filter(function (s) { return s.accepted; }).length;
        b.disabled = n === 0;
        b.textContent = n > 0
            ? 'Appliquer ' + n + ' modification' + (n > 1 ? 's' : '')
            : 'Aucune modification';
    }

    function applyAccepted() {
        if (!activeTextarea) return;
        var text = activeTextarea.value;
        pendingSuggestions.forEach(function (s) {
            if (s.accepted) text = text.replace(s.original, s.suggested);
        });
        activeTextarea.value = text;
        activeTextarea.dispatchEvent(new Event('input', { bubbles: true }));
        var inst = bootstrap.Modal.getInstance(document.getElementById('ta-improve-modal'));
        if (inst) inst.hide();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    function busy(loading, b1, b2, status, msg) {
        b1.disabled = loading;
        b2.disabled = loading;
        if (loading) {
            status.className = 'ta-status small text-muted align-self-center';
            status.innerHTML = '<span class="spinner-border spinner-border-sm me-1" style="width:.8rem;height:.8rem"></span>' + msg;
        }
    }

    function flash(status, msg, type) {
        status.className = 'ta-status small text-' + type + ' align-self-center';
        status.textContent = msg;
        setTimeout(function () { status.className = 'ta-status small align-self-center d-none'; status.textContent = ''; }, 5000);
    }

    function esc(s) {
        return (s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function post(url, data) {
        return fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        }).then(function (r) {
            if (!r.ok) return r.json().then(function (d) { return { error: d.title || d.detail || 'Erreur serveur.' }; });
            return r.json();
        });
    }

    function on(id, ev, fn) {
        var el = document.getElementById(id);
        if (el) el.addEventListener(ev, fn);
    }

    // ── Boot ──────────────────────────────────────────────────────────────────

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
