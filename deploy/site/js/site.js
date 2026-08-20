(function () {
  "use strict";

  var KEY = "vilmo_cookie_consent";
  var MAX_AGE = 60 * 60 * 24 * 365; // 12 months

  var header = document.querySelector(".header");
  if (header) {
    var onScroll = function () {
      header.classList.toggle("is-scrolled", window.scrollY > 8);
    };
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
  }

  var menuBtn = document.querySelector("[data-menu]");
  var nav = document.querySelector(".nav");
  if (menuBtn && nav) {
    menuBtn.addEventListener("click", function () {
      var open = nav.classList.toggle("is-open");
      menuBtn.setAttribute("aria-expanded", open ? "true" : "false");
    });
    nav.querySelectorAll("a").forEach(function (a) {
      a.addEventListener("click", function () {
        nav.classList.remove("is-open");
        menuBtn.setAttribute("aria-expanded", "false");
      });
    });
  }

  function readConsent() {
    try {
      var raw = localStorage.getItem(KEY);
      if (raw) return JSON.parse(raw);
    } catch (e) { /* ignore */ }
    var match = document.cookie.match(/(?:^|; )vilmo_cookie_consent=([^;]*)/);
    if (match) {
      try { return JSON.parse(decodeURIComponent(match[1])); } catch (e) { /* ignore */ }
    }
    return null;
  }

  function writeConsent(optional) {
    var rec = { v: 1, necessary: true, optional: !!optional, ts: Date.now() };
    var json = JSON.stringify(rec);
    try { localStorage.setItem(KEY, json); } catch (e) { /* ignore */ }
    document.cookie = "vilmo_cookie_consent=" + encodeURIComponent(json) +
      ";path=/;max-age=" + MAX_AGE + ";SameSite=Lax";
    return rec;
  }

  function loadOptional() {
    // v1: no analytics/marketing tags. Hook stays so Aceitar does not load third parties.
  }

  var bar = document.getElementById("cookie-bar");
  var prefs = document.getElementById("cookie-prefs");
  var optionalBox = document.getElementById("cookie-optional");

  function showBar() {
    if (bar) bar.hidden = false;
  }
  function hideBar() {
    if (bar) bar.hidden = true;
    if (prefs) prefs.hidden = true;
  }

  var existing = readConsent();
  if (existing) {
    hideBar();
    if (existing.optional) loadOptional();
  }

  document.addEventListener("click", function (ev) {
    var t = ev.target.closest("[data-cookie]");
    if (!t) return;
    var act = t.getAttribute("data-cookie");
    if (act === "accept") {
      writeConsent(true);
      loadOptional();
      hideBar();
    } else if (act === "reject") {
      writeConsent(false);
      hideBar();
    } else if (act === "prefs") {
      if (prefs) prefs.hidden = !prefs.hidden;
    } else if (act === "save-prefs") {
      writeConsent(optionalBox ? optionalBox.checked : false);
      if (optionalBox && optionalBox.checked) loadOptional();
      hideBar();
    } else if (act === "reopen") {
      ev.preventDefault();
      showBar();
      if (prefs) prefs.hidden = false;
    }
  });
})();
