document.querySelectorAll('[data-solutions-slider]').forEach(root => {
  const slides = [...root.querySelectorAll('[data-solution-slide]')];
  const dots = [...root.querySelectorAll('[data-slide-index]')];
  const pause = root.querySelector('[data-slide-pause]');
  const status = root.querySelector('[data-slide-status]');
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
  let current = 0, playing = !reduced.matches, timer;
  function show(index, announce = false) {
    current = (index + slides.length) % slides.length;
    slides.forEach((slide, i) => { slide.hidden = i !== current; });
    dots.forEach((dot, i) => dot.setAttribute('aria-pressed', String(i === current)));
    if (announce) status.textContent = slides[current].getAttribute('aria-label');
  }
  function schedule() {
    clearInterval(timer);
    pause.textContent = playing ? 'Pause slideshow' : 'Play slideshow';
    if (playing && !document.hidden && !root.matches(':hover')) timer = setInterval(() => show(current + 1), 7000);
  }
  function move(index) { playing = false; show(index, true); schedule(); }
  root.querySelector('[data-slide-prev]').addEventListener('click', () => move(current - 1));
  root.querySelector('[data-slide-next]').addEventListener('click', () => move(current + 1));
  dots.forEach((dot, i) => dot.addEventListener('click', () => move(i)));
  let pauseIntent;
  pause.addEventListener('pointerdown', () => { pauseIntent = !playing; });
  pause.addEventListener('click', () => { playing = pauseIntent ?? !playing; pauseIntent = undefined; schedule(); });
  root.addEventListener('focusin', () => { playing = false; schedule(); });
  root.addEventListener('mouseenter', () => clearInterval(timer));
  root.addEventListener('mouseleave', schedule);
  root.addEventListener('keydown', event => {
    if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
      event.preventDefault(); move(current + (event.key === 'ArrowRight' ? 1 : -1));
    }
  });
  document.addEventListener('visibilitychange', schedule);
  reduced.addEventListener('change', () => { if (reduced.matches) playing = false; schedule(); });
  root.querySelector('[data-slider-controls]').hidden = false;
  show(0); schedule();
});
