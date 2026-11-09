# histo-trainer

The repository includes a browser-based quiz deployed to [GitHub Pages](https://euan-clark-92.github.io/histo-trainer/). It uses the question pack in `content/`; practice progress is stored locally in the browser.

To run it locally, serve the repository root over HTTP (for example, `python -m http.server`) and open `http://localhost:8000/`. Opening `index.html` directly from disk will not load the question pack because the browser fetches the JSON content files.
