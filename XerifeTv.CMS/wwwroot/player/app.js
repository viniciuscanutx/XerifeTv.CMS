// ========== CONFIG ==========
const API_BASE = window.location.origin.replace(/\/$/, '');
const API_V2 = `${API_BASE}/Api/Content/v2`;
const API_V1 = `${API_BASE}/Api/Content`;

// ========== STATE ==========
let featuredData = null;

// ========== HELPERS ==========
function el(id) { return document.getElementById(id); }
function qs(sel) { return document.querySelector(sel); }
function qsa(sel) { return document.querySelectorAll(sel); }

function showLoading() { el('loading').classList.remove('hidden'); }
function hideLoading() { el('loading').classList.add('hidden'); }

async function fetchJSON(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

function placeholderImg() {
  return 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" width="180" height="270" fill="%231a1a22"><rect width="180" height="270"/></svg>';
}

function placeholderThumb() {
  return 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" width="160" height="90" fill="%231a1a22"><rect width="160" height="90"/></svg>';
}

function truncate(str, n) {
  if (!str) return '';
  return str.length > n ? str.substring(0, n) + '...' : str;
}

// ========== VIDEO URL RESOLVER ==========
async function resolveVideoUrl(resolverPath) {
  if (!resolverPath) return null;
  const url = resolverPath.startsWith('http')
    ? resolverPath
    : `${API_BASE}${resolverPath}`;
  try {
    const data = await fetchJSON(url);
    return data.url;
  } catch (e) {
    console.error('Erro ao resolver URL do vídeo:', e);
    return null;
  }
}

// ========== RENDER HOME ==========
async function loadHome() {
  showLoading();
  try {
    const data = await fetchJSON(`${API_V2}/home`);

    // Featured
    if (data.featured) {
      renderFeatured(data.featured, data.featuredType);
      featuredData = { item: data.featured, type: data.featuredType };
    }

    // Category sections
    const container = el('categorySections');
    container.innerHTML = '';

    // Movie categories
    if (data.movieCategories && data.movieCategories.length > 0) {
      for (const cat of data.movieCategories) {
        await loadCategorySection('movies', cat, container);
      }
    }

    // Series categories
    if (data.seriesCategories && data.seriesCategories.length > 0) {
      for (const cat of data.seriesCategories) {
        await loadCategorySection('series', cat, container);
      }
    }
  } catch (e) {
    console.error(e);
    container.innerHTML = '<p style="padding:2rem;text-align:center;color:#8a8a99">Erro ao carregar conteúdo.</p>';
  } finally {
    hideLoading();
  }
}

async function loadCategorySection(type, category, container) {
  try {
    const url = `${API_V2}/${type}/category/${encodeURIComponent(category)}?page=1&pageSize=15`;
    const data = await fetchJSON(url);
    const items = extractItems(data);
    if (!items || items.length === 0) return;

    const section = document.createElement('section');
    section.className = 'category-section';
    section.innerHTML = `<h2>${capitalize(category)}</h2><div class="card-row"></div>`;
    const row = section.querySelector('.card-row');

    items.forEach(item => {
      row.appendChild(createCard(item, type));
    });
    container.appendChild(section);
  } catch (e) {
    console.error(`Erro ao carregar categoria ${category}:`, e);
  }
}

function extractItems(data) {
  if (Array.isArray(data)) return data;
  if (data && data.items && Array.isArray(data.items)) return data.items;
  if (data && Array.isArray(data.items) && data.items.length && data.items[0].items)
    return data.items[0].items;
  return [];
}

// ========== RENDER FEATURED ==========
function renderFeatured(item, type) {
  el('featuredSection').classList.remove('hidden');
  const bg = item.bannerURL || item.posterURL || '';
  el('featuredBg').style.backgroundImage = `url('${bg}')`;
  el('featuredTitle').textContent = item.title || '';
  el('featuredYear').textContent = item.releaseYear || '';
  el('featuredRating').textContent = item.ratingImdb ? `${item.ratingImdb}/10` : '';
  el('featuredDuration').textContent = item.duration || (item.totalSeasons ? `${item.totalSeasons} temporadas` : '');
  el('featuredSynopsis').textContent = truncate(item.synopsis, 200);
}

el('featuredPlayBtn')?.addEventListener('click', () => {
  if (!featuredData) return;
  if (featuredData.type === 'movie') {
    openMovieModal(featuredData.item);
  } else {
    openSeriesModal(featuredData.item);
  }
});

// ========== CREATE CARD ==========
function createCard(item, type) {
  const card = document.createElement('div');
  card.className = 'card';
  card.style.position = 'relative';
  const poster = item.posterURL || placeholderImg();
  card.innerHTML = `
    <img class="card-poster" src="${poster}" alt="${escapeHtml(item.title || '')}" onerror="this.src='${placeholderImg()}'" />
    <div class="card-badge">${type === 'movies' ? 'FILME' : 'SÉRIE'}</div>
    <div class="card-title">${escapeHtml(item.title || '')}</div>
  `;
  card.addEventListener('click', () => {
    if (type === 'movies') openMovieModal(item);
    else openSeriesModal(item);
  });
  return card;
}

function escapeHtml(str) {
  if (!str) return '';
  const div = document.createElement('div');
  div.textContent = str;
  return div.innerHTML;
}

function capitalize(s) {
  if (!s) return '';
  return s.charAt(0).toUpperCase() + s.slice(1);
}

// ========== MOVIE MODAL ==========
async function openMovieModal(movie) {
  if (!movie.id && movie.id !== 0) return;

  // If we only have summary, fetch full details
  if (!movie.videoResolverURL && !movie.duration) {
    try {
      movie = await fetchJSON(`${API_V2}/movies/${movie.id}`);
    } catch (e) {
      console.error('Erro ao buscar detalhes do filme:', e);
    }
  }

  el('modalTitle').textContent = movie.title || '';
  el('modalYear').textContent = movie.releaseYear || '';
  el('modalRating').textContent = movie.ratingImdb ? `${movie.ratingImdb}/10` : '';
  el('modalDuration').textContent = movie.duration || '';
  el('modalParental').textContent = movie.parentalRating ? `${movie.parentalRating}` : '';
  el('modalSynopsis').textContent = movie.synopsis || '';

  const catContainer = el('modalCategories');
  catContainer.innerHTML = '';
  (movie.categories || []).forEach(cat => {
    const span = document.createElement('span');
    span.textContent = cat;
    catContainer.appendChild(span);
  });

  // Trailer
  const trailerEl = el('modalTrailer');
  trailerEl.innerHTML = '';
  if (movie.trailerVideoYoutubeId) {
    trailerEl.innerHTML = `<iframe src="https://www.youtube.com/embed/${movie.trailerVideoYoutubeId}" allowfullscreen></iframe>`;
  }

  // Resolve video URL
  const video = el('videoPlayer');
  video.innerHTML = '';

  // Show modal
  el('movieModal').classList.remove('hidden');

  if (movie.videoResolverURL) {
    showVideoLoading(video);
    const videoUrl = await resolveVideoUrl(movie.videoResolverURL);
    clearVideoLoading(video);
    if (videoUrl) {
      setupVideoSource(video, videoUrl, movie.subtitleURL);
    } else {
      video.poster = movie.posterURL || '';
      video.setAttribute('controls', false);
    }
  } else {
    video.poster = movie.posterURL || '';
  }
}

function showVideoLoading(videoEl) {
  // Could show a spinner overlay here
}
function clearVideoLoading(videoEl) {
  // Clear spinner
}

function setupVideoSource(video, url, subtitleUrl) {
  let sourceEl = video.querySelector('source');
  if (!sourceEl) {
    sourceEl = document.createElement('source');
    video.appendChild(sourceEl);
  }
  if (subtitleUrl) {
    const track = document.createElement('track');
    track.kind = 'subtitles';
    track.src = subtitleUrl;
    track.srclang = 'pt';
    track.label = 'Português';
    track.default = true;
    // Remove existing tracks
    video.querySelectorAll('track').forEach(t => t.remove());
    video.appendChild(track);
  }
  sourceEl.src = url;
  video.load();
  video.play().catch(e => console.log('Autoplay preventido:', e));
}

// ========== SERIES MODAL ==========
async function openSeriesModal(series) {
  if (!series.id && series.id !== 0) return;

  // Fetch full details if needed
  if (!series.totalSeasons && series.id) {
    try {
      series = await fetchJSON(`${API_V2}/series/${series.id}`);
    } catch (e) {
      console.error('Erro ao buscar detalhes da serie:', e);
    }
  }

  el('seriesTitle').textContent = series.title || '';
  el('seriesYear').textContent = series.releaseYear || '';
  el('seriesRating').textContent = series.ratingImdb ? `${series.ratingImdb}/10` : '';
  el('seriesSeasons').textContent = series.totalSeasons ? `${series.totalSeasons} temp.` : '';
  el('seriesParental').textContent = series.parentalRating || '';
  el('seriesSynopsis').textContent = series.synopsis || '';

  const poster = el('seriesPoster');
  poster.src = series.posterURL || placeholderImg();
  poster.onerror = () => { poster.src = placeholderImg(); };

  const catContainer = el('seriesCategories');
  catContainer.innerHTML = '';
  (series.categories || []).forEach(cat => {
    const span = document.createElement('span');
    span.textContent = cat;
    catContainer.appendChild(span);
  });

  // Hide episode player if open
  el('episodePlayerWrapper').classList.add('hidden');
  el('seasonsBar').innerHTML = '';
  el('episodesList').innerHTML = '';

  // Season buttons
  const numSeasons = series.totalSeasons || 1;
  for (let s = 1; s <= numSeasons; s++) {
    const btn = document.createElement('button');
    btn.className = 'season-btn';
    btn.textContent = `Temp ${s}`;
    btn.addEventListener('click', () => {
      qsa('.season-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      loadEpisodes(series.id, s);
    });
    el('seasonsBar').appendChild(btn);
  }

  el('seriesModal').classList.remove('hidden');

  // Load first season
  if (numSeasons > 0) {
    el('seasonsBar').querySelector('.season-btn')?.classList.add('active');
    loadEpisodes(series.id, 1);
  }
}

async function loadEpisodes(seriesId, season) {
  const list = el('episodesList');
  list.innerHTML = '<p style="color:#8a8a99;padding:1rem;">Carregando episodios...</p>';

  try {
    const data = await fetchJSON(`${API_V2}/series/${seriesId}/seasons/${season}/episodes`);
    const episodes = data.episodes || [];

    if (episodes.length === 0) {
      list.innerHTML = '<p style="color:#8a8a99;padding:1rem;">Nenhum episodio encontrado.</p>';
      return;
    }

    list.innerHTML = '';
    episodes.forEach((ep, idx) => {
      const item = document.createElement('div');
      item.className = 'episode-item';
      const thumb = ep.bannerURL || placeholderThumb();
      item.innerHTML = `
        <img class="episode-thumb" src="${thumb}" alt="" onerror="this.src='${placeholderThumb()}'" />
        <div class="episode-info">
          <div class="episode-num">EP ${idx + 1}</div>
          <div class="episode-title">${escapeHtml(ep.title || `Episodio ${idx + 1}`)}</div>
          <div class="episode-duration">${ep.duration || ''}</div>
        </div>
      `;
      item.addEventListener('click', () => playEpisode(ep));
      list.appendChild(item);
    });
  } catch (e) {
    console.error('Erro ao carregar episodios:', e);
    list.innerHTML = '<p style="color:#8a8a99;padding:1rem;">Erro ao carregar episodios.</p>';
  }
}

async function playEpisode(episode) {
  const wrapper = el('episodePlayerWrapper');
  const video = el('episodeVideoPlayer');
  video.innerHTML = '';

  wrapper.classList.remove('hidden');
  wrapper.scrollIntoView({ behavior: 'smooth' });

  if (episode.videoResolverURL) {
    const videoUrl = await resolveVideoUrl(episode.videoResolverURL);
    if (videoUrl) {
      setupVideoSource(video, videoUrl, episode.subtitleURL);
    }
  }
}

el('closeEpisodePlayer')?.addEventListener('click', () => {
  const wrapper = el('episodePlayerWrapper');
  const video = el('episodeVideoPlayer');
  video.pause();
  video.removeAttribute('src');
  video.load();
  wrapper.classList.add('hidden');
});

// ========== SEARCH ==========
async function doSearch() {
  const term = el('searchInput').value.trim();
  if (!term) return;

  el('searchTerm').textContent = term;
  el('searchModal').classList.remove('hidden');
  const resultsEl = el('searchResults');
  resultsEl.innerHTML = '<p style="color:#8a8a99;padding:1rem;">Buscando...</p>';

  try {
    const data = await fetchJSON(`${API_V2}/search?term=${encodeURIComponent(term)}`);

    resultsEl.innerHTML = '';

    // Movies
    if (data.movies && data.movies.length > 0) {
      const sec = document.createElement('div');
      sec.className = 'search-section';
      sec.innerHTML = '<h3>Filmes</h3>';
      data.movies.forEach(movie => {
        const card = document.createElement('div');
        card.className = 'search-result-card';
        card.innerHTML = `
          <img src="${movie.posterURL || placeholderImg()}" alt="" onerror="this.src='${placeholderImg()}'" />
          <div class="search-result-info">
            <h4>${escapeHtml(movie.title || '')}</h4>
            <span>${movie.releaseYear || ''} ${movie.duration ? ' - ' + movie.duration : ''}</span>
          </div>
        `;
        card.addEventListener('click', () => {
          closeAllModals();
          openMovieModal(movie);
        });
        sec.appendChild(card);
      });
      resultsEl.appendChild(sec);
    }

    // Series
    if (data.series && data.series.length > 0) {
      const sec = document.createElement('div');
      sec.className = 'search-section';
      sec.innerHTML = '<h3>Series</h3>';
      data.series.forEach(series => {
        const card = document.createElement('div');
        card.className = 'search-result-card';
        card.innerHTML = `
          <img src="${series.posterURL || placeholderImg()}" alt="" onerror="this.src='${placeholderImg()}'" />
          <div class="search-result-info">
            <h4>${escapeHtml(series.title || '')}</h4>
            <span>${series.releaseYear || ''} ${series.totalSeasons ? ' - ' + series.totalSeasons + ' temp.' : ''}</span>
          </div>
        `;
        card.addEventListener('click', () => {
          closeAllModals();
          openSeriesModal(series);
        });
        sec.appendChild(card);
      });
      resultsEl.appendChild(sec);
    }

    if (resultsEl.innerHTML === '') {
      resultsEl.innerHTML = '<p style="color:#8a8a99;padding:1rem;">Nenhum resultado encontrado.</p>';
    }
  } catch (e) {
    console.error(e);
    resultsEl.innerHTML = '<p style="color:#8a8a99;padding:1rem;">Erro na busca.</p>';
  }
}

el('searchBtn')?.addEventListener('click', doSearch);
el('searchInput')?.addEventListener('keypress', (e) => {
  if (e.key === 'Enter') doSearch();
});

// ========== TABS ==========
qsa('.nav-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    qsa('.nav-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    const tab = btn.dataset.tab;
    el('featuredSection').classList.add('hidden');
    el('categorySections').innerHTML = '';

    if (tab === 'home') {
      loadHome();
    } else if (tab === 'movies') {
      loadAllMovies();
    } else if (tab === 'series') {
      loadAllSeries();
    }
  });
});

async function loadAllMovies() {
  showLoading();
  try {
    const data = await fetchJSON(`${API_V2}/movies`);
    const items = extractItems(data);
    const container = el('categorySections');
    container.innerHTML = '';
    const section = document.createElement('section');
    section.className = 'category-section';
    section.innerHTML = '<h2>Todos os Filmes</h2><div class="card-row" style="flex-wrap:wrap;"></div>';
    const row = section.querySelector('.card-row');
    items.forEach(item => row.appendChild(createCard(item, 'movies')));
    container.appendChild(section);
  } catch (e) {
    console.error(e);
  } finally {
    hideLoading();
  }
}

async function loadAllSeries() {
  showLoading();
  try {
    const data = await fetchJSON(`${API_V2}/series`);
    const items = extractItems(data);
    const container = el('categorySections');
    container.innerHTML = '';
    const section = document.createElement('section');
    section.className = 'category-section';
    section.innerHTML = '<h2>Todas as Series</h2><div class="card-row" style="flex-wrap:wrap;"></div>';
    const row = section.querySelector('.card-row');
    items.forEach(item => row.appendChild(createCard(item, 'series')));
    container.appendChild(section);
  } catch (e) {
    console.error(e);
  } finally {
    hideLoading();
  }
}

// ========== MODAL CLOSE ==========
function closeAllModals() {
  el('movieModal').classList.add('hidden');
  el('seriesModal').classList.add('hidden');
  el('searchModal').classList.add('hidden');

  // Pause videos
  const vp = el('videoPlayer');
  vp.pause();
  vp.removeAttribute('src');
  vp.load();

  const evp = el('episodeVideoPlayer');
  evp.pause();
  evp.removeAttribute('src');
  evp.load();
}

document.addEventListener('click', (e) => {
  if (e.target.hasAttribute('data-close')) {
    closeAllModals();
  }
});

document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') closeAllModals();
});

// ========== INIT ==========
loadHome();
