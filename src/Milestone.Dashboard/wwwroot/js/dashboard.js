const state = {
  cameras: [],
  map: null,
  markers: []
};

const refreshButton = document.getElementById("refresh-btn");
const searchInput = document.getElementById("search");
const serverFilter = document.getElementById("server-filter");
const locationFilter = document.getElementById("location-filter");

refreshButton.addEventListener("click", () => loadDashboard());
searchInput.addEventListener("input", renderCameras);
serverFilter.addEventListener("change", renderCameras);
locationFilter.addEventListener("change", renderCameras);

async function loadDashboard() {
  refreshButton.disabled = true;
  try {
    const response = await fetch("/api/dashboard", { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`Dashboard API returned ${response.status}`);
    }

    const data = await response.json();
    state.cameras = data.cameras ?? [];
    renderSource(data);
    renderKpis(data.summary ?? {});
    renderStorage(data.storages ?? []);
    fillServerFilter(data.recordingServers ?? []);
    renderCameras();
    renderMap(state.cameras);
  } catch (error) {
    document.getElementById("source-badge").textContent = "Unavailable";
    document.getElementById("kpi-row").innerHTML = `<article class="kpi"><div class="label">Error</div><div class="value">Could not load dashboard</div><div class="hint muted">${error.message}</div></article>`;
  } finally {
    refreshButton.disabled = false;
  }
}

function renderSource(data) {
  const badge = document.getElementById("source-badge");
  const source = data.source ?? "unknown";
  badge.textContent = source === "demo" ? "Demo data" : source;
  badge.className = `badge ${source === "demo" ? "demo" : "live"}`;
  document.getElementById("generated-at").textContent = data.generatedAt
    ? `Updated ${new Date(data.generatedAt).toLocaleString()}`
    : "";
}

function renderKpis(summary) {
  const cards = [
    ["Cameras", summary.cameraCount ?? 0, `${summary.enabledCameraCount ?? 0} enabled`],
    ["Mapped", summary.mappedCameraCount ?? 0, `${summary.unmappedCameraCount ?? 0} still need coordinates`],
    ["Recording servers", summary.recordingServerCount ?? 0, `${summary.storageCount ?? 0} storage volumes`],
    ["Storage used", formatPercent(summary.storageUsagePercent), `${formatMb(summary.usedSpaceMb)} of ${formatMb(summary.maxSizeMb)}`]
  ];

  document.getElementById("kpi-row").innerHTML = cards.map(([label, value, hint]) => `
    <article class="kpi">
      <div class="label">${label}</div>
      <div class="value">${value}</div>
      <div class="hint muted">${hint}</div>
    </article>
  `).join("");
}

function renderStorage(storages) {
  document.getElementById("storage-list").innerHTML = storages.map((storage) => {
    const tone = storage.usagePercent >= 90 ? "bad" : storage.usagePercent >= 75 ? "warn" : "";
    return `
      <section class="storage-card">
        <header>
          <div>
            <strong>${escapeHtml(storage.name)}</strong>
            <div class="muted">${escapeHtml(storage.recordingServerName ?? "")} · ${escapeHtml(storage.kind)}</div>
          </div>
          <div>${formatPercent(storage.usagePercent)}</div>
        </header>
        <div class="bar ${tone}"><span style="width:${Math.min(storage.usagePercent, 100)}%"></span></div>
        <p class="muted">${escapeHtml(storage.usedLabel)} / ${escapeHtml(storage.maxLabel)} · Retention ${escapeHtml(storage.retentionLabel)}</p>
        <p class="muted">${escapeHtml(storage.diskPath ?? "Path not reported")}${storage.lockedUsedSpaceMb ? ` · Locked ${formatMb(storage.lockedUsedSpaceMb)}` : ""}</p>
      </section>
    `;
  }).join("") || `<p class="muted">No storage volumes were returned.</p>`;
}

function fillServerFilter(servers) {
  const current = serverFilter.value;
  serverFilter.innerHTML = `<option value="">All recording servers</option>` +
    servers.map((server) => `<option value="${escapeHtml(server.id)}">${escapeHtml(server.name)}</option>`).join("");
  serverFilter.value = current;
}

function renderCameras() {
  const query = searchInput.value.trim().toLowerCase();
  const rows = state.cameras.filter((camera) => {
    const haystack = [camera.name, camera.site, camera.recordingServerName, camera.hardwareAddress]
      .filter(Boolean)
      .join(" ")
      .toLowerCase();
    const matchesQuery = !query || haystack.includes(query);
    const matchesServer = !serverFilter.value || camera.recordingServerId === serverFilter.value;
    const mapped = Boolean(camera.location);
    const matchesLocation = !locationFilter.value
      || (locationFilter.value === "mapped" && mapped)
      || (locationFilter.value === "unmapped" && !mapped);
    return matchesQuery && matchesServer && matchesLocation;
  });

  document.getElementById("camera-body").innerHTML = rows.map((camera) => `
    <tr>
      <td>${escapeHtml(camera.name)}</td>
      <td>${escapeHtml(camera.site ?? "—")}</td>
      <td>${escapeHtml(camera.recordingServerName ?? "—")}</td>
      <td>${escapeHtml(camera.hardwareAddress ?? "—")}</td>
      <td><span class="status ${camera.enabled ? "on" : "off"}">${camera.enabled ? "Enabled" : "Disabled"}</span></td>
      <td>${formatLocation(camera)}</td>
    </tr>
  `).join("") || `<tr><td colspan="6">No cameras match the current filters.</td></tr>`;
}

function renderMap(cameras) {
  if (!state.map) {
    state.map = L.map("map", { zoomControl: true }).setView([34.0522, -118.2437], 13);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors"
    }).addTo(state.map);
  }

  state.markers.forEach((marker) => marker.remove());
  state.markers = [];
  const bounds = [];

  cameras.filter((camera) => camera.location).forEach((camera) => {
    const point = [camera.location.latitude, camera.location.longitude];
    const marker = L.marker(point).addTo(state.map).bindPopup(
      `<strong>${escapeHtml(camera.name)}</strong><br>${escapeHtml(camera.site ?? "")}<br>${escapeHtml(camera.recordingServerName ?? "")}`
    );
    state.markers.push(marker);
    bounds.push(point);
  });

  if (bounds.length > 0) {
    state.map.fitBounds(bounds, { padding: [28, 28], maxZoom: 15 });
  }

  setTimeout(() => state.map.invalidateSize(), 80);
}

function formatLocation(camera) {
  if (!camera.location) {
    return "Not mapped";
  }

  const suffix = camera.locationIsOverride ? " (override)" : "";
  return `${camera.location.latitude.toFixed(5)}, ${camera.location.longitude.toFixed(5)}${suffix}`;
}

function formatPercent(value) {
  return `${Number(value ?? 0).toFixed(1)}%`;
}

function formatMb(value) {
  const mb = Number(value ?? 0);
  if (mb < 1024) {
    return `${mb.toLocaleString()} MB`;
  }
  const gb = mb / 1024;
  return gb < 1024 ? `${gb.toFixed(1)} GB` : `${(gb / 1024).toFixed(2)} TB`;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

loadDashboard();
