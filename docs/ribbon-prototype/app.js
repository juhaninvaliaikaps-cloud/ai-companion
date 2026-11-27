(() => {
  const params = new URLSearchParams(window.location.search);
  const state = params.get("state") || "menu";
  const capture = params.get("capture") === "1";
  const body = document.body;
  const stage = document.getElementById("stage");
  const splash = document.getElementById("splash");
  const toast = document.getElementById("toast");
  const voiceButton = document.getElementById("voice-button");
  const controls = document.getElementById("controls");
  const profileRow = document.querySelector('[data-action="profile"]');
  const aiRow = document.querySelector('[data-action="ai"]');
  let flyoutTimer = 0;
  let splashTimer = 0;
  let toastTimer = 0;

  if (capture) body.classList.add("capture");

  function say(text) {
    clearTimeout(toastTimer);
    if (!text) {
      toast.classList.remove("show");
      return;
    }
    toast.textContent = text;
    toast.classList.add("show");
    toastTimer = setTimeout(() => toast.classList.remove("show"), 2200);
  }

  function closeFlyout() {
    body.dataset.flyout = "";
    document.querySelectorAll(".menu-item").forEach((row) => row.classList.remove("is-open"));
  }

  function openFlyout(name) {
    clearTimeout(flyoutTimer);
    body.dataset.flyout = name;
    document.querySelectorAll(".menu-item").forEach((row) => row.classList.remove("is-open"));
    const row = name === "profile" ? profileRow : aiRow;
    if (row) row.classList.add("is-open");
  }

  function scheduleClose() {
    clearTimeout(flyoutTimer);
    flyoutTimer = setTimeout(closeFlyout, 220);
  }

  function playSplash() {
    clearTimeout(splashTimer);
    splash.classList.remove("play");
    void splash.offsetWidth;
    splash.classList.add("play");
    splashTimer = setTimeout(() => splash.classList.remove("play"), 760);
  }

  function setMuted(muted) {
    body.classList.toggle("muted", muted);
    const label = document.querySelector('[data-action="mute"] .menu-label');
    if (label) label.textContent = muted ? "Unmute" : "Mute";
    const note = document.getElementById("voice-note");
    if (note) note.textContent = muted ? "Voice muted" : "Voice on";
    voiceButton.setAttribute("aria-label", muted ? "Unmute voice" : "Mute voice");
  }

  function setState(name) {
    body.dataset.state = name;
    closeFlyout();
    document.querySelectorAll(".menu-item").forEach((row) => row.classList.remove("is-hover"));
    if (name === "profile") openFlyout("profile");
    if (name === "ai") openFlyout("ai");
    if (name === "hover") profileRow.classList.add("is-hover");
    setMuted(name === "muted");
    if (name === "accent") {
      body.dataset.flyout = "";
      say("Action selected — ink accent plays once.");
    }
    if (name === "menu") say("");
    document.querySelectorAll("[data-state-button]").forEach((button) => {
      button.classList.toggle("active", button.dataset.stateButton === name);
    });
  }

  document.querySelectorAll("[data-action]").forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.action;
      if (action === "mute") {
        setMuted(!body.classList.contains("muted"));
        say(body.classList.contains("muted") ? "Voice muted (header reflects it)." : "Voice on.");
        return;
      }
      if (action === "profile" || action === "ai") {
        openFlyout(action);
        return;
      }
      if (action === "look") {
        playSplash();
        say("Look now — inert in the visual prototype.");
        return;
      }
      if (action === "settings") {
        playSplash();
        say("Settings — inert in the visual prototype.");
        return;
      }
      if (action === "close") {
        closeFlyout();
        say("Close — dismisses the flyout in the prototype.");
        return;
      }
      if (action === "send") {
        playSplash();
        say("Send — inert in the visual prototype.");
        return;
      }
      playSplash();
      say("Destination is inert in this preview.");
    });
  });

  [profileRow, aiRow].forEach((row) => {
    if (!row) return;
    row.addEventListener("mouseenter", () => {
      if (row.dataset.action === "profile") openFlyout("profile");
      else openFlyout("ai");
    });
    row.addEventListener("mouseleave", scheduleClose);
  });

  document.querySelectorAll(".flyout").forEach((flyout) => {
    flyout.addEventListener("mouseenter", () => clearTimeout(flyoutTimer));
    flyout.addEventListener("mouseleave", scheduleClose);
  });

  controls.addEventListener("click", (event) => {
    const button = event.target.closest("[data-state-button]");
    if (!button) return;
    setState(button.dataset.stateButton);
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") closeFlyout();
  });

  setState(state);
})();
