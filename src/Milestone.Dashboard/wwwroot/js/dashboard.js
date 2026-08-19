const state = {
  cameras: [],
  map: null,
  markers: [],
  mapCenter: { latitude: 34.0522, longitude: -118.2437, zoom: 13 },
  placingCameraId: ""
};

const refreshButton = document.getElementById("refresh-btn");
const searchInput = document.getElementById("search");
const serverFilter = document.getElementById("server-filter");
const locationFilter = document.getElementById("location-filter");
const placeCamera = document.getElementById("place-camera");
const placeHint = document.getElementById("place-hint");
const addressSearch = document.getElementById("address-search");
const addressSearchButton = document.getElementById("address-search-btn");
const csvImport = document.getElementById("csv-import");

refreshButton.addEventListener("click", () => loadDashboard());
searchInput.addEventListener("input", renderCameras);
serverFilter.addEventListener("change", renderCameras);
locationFilter.addEventListener("change", renderCameras);
placeCamera.addEventListener("change", () => {
  state.placingCameraId = placeCamera.value;
  updatePlaceHint();
});
addressSearchButton.addEventListener("click", searchAddress);
addressSearch.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    event.preventDefault();
    searchAddress();
  }
});
csvImport.addEventListener("change", importCsv);

async function loadDashboard() {
  refreshButton.disabled = true;
  try {
    const response = await fetch("/api/dashboard", { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`Dashboard API returned ${response.status}`);
    }

    const data = await response.json();
    state.cameras = data.cameras ?? [];
    state.mapCenter = data.mapCenter ?? state.mapCenter;
    renderSource(data);
    renderKpis(data.summary ?? {});
    renderStorage(data.storages ?? []);
    fillServerFilter(data.recordingServers ?? []);
    fillPlaceCamera();
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

function fillPlaceCamera() {
  const current = state.placingCameraId;
  placeCamera.innerHTML = `<option value="">Select camera to place</option>` +
    state.cameras.map((camera) => {
      const suffix = camera.location ? " (mapped)" : "";
      return `<option value="${escapeHtml(camera.id)}">${escapeHtml(camera.name)}${suffix}</option>`;
    }).join("");
  placeCamera.value = current;
  state.placingCameraId = placeCamera.value;
  updatePlaceHint();
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
      <td><button class="link-btn" type="button" data-place="${escapeHtml(camera.id)}">Place on map</button></td>
    </tr>
  `).join("") || `<tr><td colspan="7">No cameras match the current filters.</td></tr>`;

  document.querySelectorAll("[data-place]").forEach((button) => {
    button.addEventListener("click", () => {
      state.placingCameraId = button.getAttribute("data-place") ?? "";
      placeCamera.value = state.placingCameraId;
      updatePlaceHint();
      document.getElementById("map").scrollIntoView({ behavior: "smooth", block: "center" });
    });
  });
}

function renderMap(cameras) {
  const center = [state.mapCenter.latitude, state.mapCenter.longitude];
  if (!state.map) {
    state.map = L.map("map", { zoomControl: true }).setView(center, state.mapCenter.zoom ?? 13);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors"
    }).addTo(state.map);
    state.map.on("click", onMapClick);
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
    state.map.fitBounds(bounds, { padding: [28, 28], maxZoom: 16 });
  } else {
    state.map.setView(center, state.mapCenter.zoom ?? 13);
  }

  setTimeout(() => state.map.invalidateSize(), 80);
}

async function onMapClick(event) {
  if (!state.placingCameraId) {
    placeHint.textContent = "Select a camera first, then click the map.";
    return;
  }

  const camera = state.cameras.find((item) => item.id === state.placingCameraId);
  const response = await fetch("/api/locations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      cameraId: state.placingCameraId,
      latitude: event.latlng.lat,
      longitude: event.latlng.lng,
      site: camera?.site ?? ""
    })
  });

  if (!response.ok) {
    placeHint.textContent = "Could not save that camera location.";
    return;
  }

  placeHint.textContent = `Saved ${camera?.name ?? "camera"} on the map.`;
  await loadDashboard();
}

async function searchAddress() {
  const query = addressSearch.value.trim();
  if (!query) {
    return;
  }

  const response = await fetch(`/api/geocode?q=${encodeURIComponent(query)}`);
  if (!response.ok) {
    placeHint.textContent = "Address search failed. Check internet access from the web server.";
    return;
  }

  const results = await response.json();
  if (!results.length) {
    placeHint.textContent = "No matching address was found.";
    return;
  }

  const match = results[0];
  state.map.setView([match.latitude, match.longitude], 16);
  placeHint.textContent = `Map moved to ${match.label}. Select a camera and click to pin it.`;
}

async function importCsv(event) {
  const file = event.target.files?.[0];
  event.target.value = "";
  if (!file) {
    return;
  }

  const text = await file.text();
  const rows = parseCsv(text);
  if (rows.length === 0) {
    placeHint.textContent = "The CSV had no location rows.";
    return;
  }

  const response = await fetch("/api/locations/import", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(rows)
  });

  if (!response.ok) {
    placeHint.textContent = "CSV import failed. Use cameraId or name, latitude, and longitude columns.";
    return;
  }

  const result = await response.json();
  const extra = result.unmatched?.length ? ` Unmatched: ${result.unmatched.join(", ")}` : "";
  placeHint.textContent = `Imported ${result.saved} camera location(s).${extra}`;
  await loadDashboard();
}

function parseCsv(text) {
  const lines = text.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
  if (lines.length < 2) {
    return [];
  }

  const headers = splitCsvLine(lines[0]).map((header) => header.toLowerCase());
  const index = (name) => headers.indexOf(name);
  return lines.slice(1).map((line) => {
    const cells = splitCsvLine(line);
    return {
      cameraId: cells[index("cameraid")] || "",
      name: cells[index("name")] || "",
      latitude: Number(cells[index("latitude")]),
      longitude: Number(cells[index("longitude")]),
      site: cells[index("site")] || ""
    };
  }).filter((row) => Number.isFinite(row.latitude) && Number.isFinite(row.longitude));
}

function splitCsvLine(line) {
  const values = [];
  let current = "";
  let quoted = false;
  for (const character of line) {
    if (character === "\"") {
      quoted = !quoted;
      continue;
    }
    if (character === "," && !quoted) {
      values.push(current.trim());
      current = "";
      continue;
    }
    current += character;
  }
  values.push(current.trim());
  return values;
}

function updatePlaceHint() {
  const map = document.getElementById("map");
  if (!state.placingCameraId) {
    placeHint.textContent = "Select a camera, then click the map.";
    map.classList.remove("placing");
    return;
  }

  const camera = state.cameras.find((item) => item.id === state.placingCameraId);
  placeHint.textContent = `Click the map to place ${camera?.name ?? "the selected camera"}.`;
  map.classList.add("placing");
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
