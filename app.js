const STORAGE_KEY = "histology-trainer-progress-v1";

const elements = {
  home: document.querySelector("#home-screen"),
  quiz: document.querySelector("#quiz-screen"),
  results: document.querySelector("#result-screen"),
  status: document.querySelector("#status-message"),
  questionCount: document.querySelector("#question-count"),
  sessionLength: document.querySelector("#session-length"),
  weakDescription: document.querySelector("#weak-description"),
  startPractice: document.querySelector("#start-practice"),
  startWeak: document.querySelector("#start-weak"),
  startAssessment: document.querySelector("#start-assessment"),
  quitQuiz: document.querySelector("#quit-quiz"),
  mode: document.querySelector("#session-mode"),
  questionProgress: document.querySelector("#question-progress"),
  progressTrack: document.querySelector(".progress-track"),
  progressFill: document.querySelector("#progress-fill"),
  image: document.querySelector("#question-image"),
  zoomImage: document.querySelector("#zoom-image"),
  categories: document.querySelector("#question-categories"),
  prompt: document.querySelector("#quiz-prompt"),
  answerOptions: document.querySelector("#answer-options"),
  answerForm: document.querySelector("#answer-form"),
  submitAnswer: document.querySelector("#submit-answer"),
  nextQuestion: document.querySelector("#next-question"),
  feedback: document.querySelector("#feedback-panel"),
  feedbackResult: document.querySelector("#feedback-result"),
  feedbackAnswer: document.querySelector("#feedback-answer"),
  feedbackExplanation: document.querySelector("#feedback-explanation"),
  feedbackFeatures: document.querySelector("#feedback-features"),
  resultMode: document.querySelector("#result-mode"),
  resultScore: document.querySelector("#result-score"),
  resultDuration: document.querySelector("#result-duration"),
  resultRecommendation: document.querySelector("#result-recommendation"),
  answerReview: document.querySelector("#answer-review"),
  backHome: document.querySelector("#back-home"),
  imageDialog: document.querySelector("#image-dialog"),
  largeImage: document.querySelector("#large-image"),
  closeImage: document.querySelector("#close-image")
};

const app = {
  questions: [],
  categories: [],
  passPercent: 80,
  progress: { questions: {}, categories: {} },
  storageAvailable: true,
  session: null
};

const modeLabels = {
  practice: "Daily practice",
  weak: "Practice weak areas",
  assessment: "Assessment"
};

function showStatus(message, isError = false) {
  elements.status.textContent = message;
  elements.status.classList.toggle("is-error", isError);
  elements.status.hidden = !message;
}

function loadProgress() {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      const progress = JSON.parse(saved);
      if (progress && typeof progress === "object") {
        app.progress = {
          questions: progress.questions && typeof progress.questions === "object" ? progress.questions : {},
          categories: progress.categories && typeof progress.categories === "object" ? progress.categories : {}
        };
      }
    }
  } catch {
    app.storageAvailable = false;
    showStatus("Saved progress could not be read. You can still use the quiz, but your results will not be available for weak-area practice.", true);
  }
}

function saveProgress() {
  if (!app.storageAvailable) return;
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(app.progress));
  } catch {
    app.storageAvailable = false;
    showStatus("This browser could not save quiz progress. You can continue studying, but results will not be saved.", true);
  }
}

function shuffle(items) {
  const shuffled = [...items];
  for (let index = shuffled.length - 1; index > 0; index -= 1) {
    const swapIndex = Math.floor(Math.random() * (index + 1));
    [shuffled[index], shuffled[swapIndex]] = [shuffled[swapIndex], shuffled[index]];
  }
  return shuffled;
}

function questionCount() {
  return Math.min(Number(elements.sessionLength.value), app.questions.length);
}

function updateHome() {
  elements.questionCount.textContent = `${app.questions.length} questions ready`;
  elements.sessionLength.replaceChildren();
  const values = [...new Set([Math.min(5, app.questions.length), app.questions.length])];
  for (const value of values) {
    const option = document.createElement("option");
    option.value = String(value);
    option.textContent = value === app.questions.length ? "All questions" : `${value} questions`;
    elements.sessionLength.append(option);
  }
  elements.sessionLength.value = String(app.questions.length);

  const weakest = Object.entries(app.progress.categories)
    .filter(([, stats]) => stats.attempts > stats.correct)
    .sort((left, right) => left[1].correct / left[1].attempts - right[1].correct / right[1].attempts)[0];
  elements.weakDescription.textContent = weakest
    ? `Your recent results show ${categoryLabel(weakest[0])} could use a little more practice.`
    : "Focus on categories where you have missed questions. Your progress stays on this device.";
}

function categoryLabel(categoryId) {
  return app.categories.find(category => category.id === categoryId)?.label ?? categoryId.replaceAll("_", " ");
}

function beginSession(mode) {
  if (app.questions.length === 0) return;
  const count = questionCount();
  let pool = app.questions;

  if (mode === "weak") {
    const weakCategories = Object.entries(app.progress.categories)
      .filter(([, stats]) => stats.attempts > stats.correct)
      .sort((left, right) => left[1].correct / left[1].attempts - right[1].correct / right[1].attempts)
      .slice(0, 3)
      .map(([categoryId]) => categoryId);
    if (weakCategories.length > 0) {
      const focused = app.questions.filter(question => question.categories.some(category => weakCategories.includes(category)));
      if (focused.length > 0) pool = focused;
    }
  }

  app.session = {
    mode,
    questions: shuffle(pool).slice(0, count),
    answers: [],
    index: 0,
    startedAt: Date.now(),
    questionStartedAt: Date.now()
  };
  showScreen("quiz");
  elements.mode.textContent = modeLabels[mode];
  renderQuestion();
}

function showScreen(screenName) {
  elements.home.hidden = screenName !== "home";
  elements.quiz.hidden = screenName !== "quiz";
  elements.results.hidden = screenName !== "results";
  if (screenName === "home") updateHome();
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function imageUrl(question) {
  return new URL(`./content/${question.image}`, document.baseURI).href;
}

function renderQuestion() {
  const session = app.session;
  const question = session.questions[session.index];
  const position = session.index + 1;
  const percent = Math.round(((position - 1) / session.questions.length) * 100);

  elements.questionProgress.textContent = `${position} / ${session.questions.length}`;
  elements.progressTrack.setAttribute("aria-valuenow", String(percent));
  elements.progressFill.style.width = `${percent}%`;
  elements.categories.textContent = question.categories.map(categoryLabel).join(" · ");
  elements.prompt.textContent = question.prompt;
  elements.image.src = imageUrl(question);
  elements.image.alt = `Histology specimen: ${question.prompt}`;
  elements.image.onerror = () => showStatus(`The image for question ${question.id} could not be loaded.`, true);
  elements.feedback.hidden = true;
  elements.nextQuestion.hidden = true;
  elements.submitAnswer.hidden = false;
  elements.submitAnswer.disabled = true;
  elements.submitAnswer.innerHTML = 'Check answer <span aria-hidden="true">→</span>';
  elements.answerOptions.replaceChildren(elements.answerOptions.querySelector("legend"));
  session.questionStartedAt = Date.now();

  question.options.forEach((option, index) => {
    const label = document.createElement("label");
    label.className = "option-label";
    const input = document.createElement("input");
    input.type = "radio";
    input.name = "answer";
    input.value = String(index);
    input.addEventListener("change", () => {
      elements.submitAnswer.disabled = false;
    });
    const text = document.createElement("span");
    text.textContent = option;
    label.append(input, text);
    elements.answerOptions.append(label);
  });
  elements.prompt.focus({ preventScroll: true });
}

function submitAnswer(event) {
  event.preventDefault();
  const session = app.session;
  const question = session.questions[session.index];
  const selected = elements.answerOptions.querySelector('input[name="answer"]:checked');
  if (!selected || session.answers.length > session.index) return;

  const selectedIndex = Number(selected.value);
  const correct = selectedIndex === question.correct_index;
  const answer = { question, selectedIndex, correct };
  session.answers.push(answer);
  recordAttempt(question, correct);

  for (const label of elements.answerOptions.querySelectorAll(".option-label")) {
    const input = label.querySelector("input");
    input.disabled = true;
    label.classList.add("is-disabled");
    if (Number(input.value) === question.correct_index) label.classList.add("is-correct");
    if (Number(input.value) === selectedIndex && !correct) label.classList.add("is-incorrect");
  }
  elements.submitAnswer.hidden = true;
  elements.nextQuestion.hidden = false;
  const isLast = session.index === session.questions.length - 1;
  elements.nextQuestion.innerHTML = isLast
    ? `Finish ${session.mode === "assessment" ? "assessment" : "session"} <span aria-hidden="true">→</span>`
    : 'Next question <span aria-hidden="true">→</span>';

  if (session.mode !== "assessment") showFeedback(question, correct);
}

function showFeedback(question, correct) {
  elements.feedback.hidden = false;
  elements.feedbackResult.textContent = correct ? "Correct" : "Not quite";
  elements.feedbackResult.className = `feedback-result ${correct ? "is-correct" : "is-incorrect"}`;
  elements.feedbackAnswer.textContent = `Correct answer: ${question.options[question.correct_index]}`;
  elements.feedbackExplanation.textContent = question.explanation;
  elements.feedbackFeatures.replaceChildren();
  for (const feature of question.features ?? []) {
    const item = document.createElement("li");
    item.textContent = feature;
    elements.feedbackFeatures.append(item);
  }
}

function recordAttempt(question, correct) {
  const questionStats = app.progress.questions[question.id] ?? { attempts: 0, correct: 0 };
  questionStats.attempts += 1;
  if (correct) questionStats.correct += 1;
  app.progress.questions[question.id] = questionStats;

  for (const categoryId of question.categories) {
    const categoryStats = app.progress.categories[categoryId] ?? { attempts: 0, correct: 0 };
    categoryStats.attempts += 1;
    if (correct) categoryStats.correct += 1;
    app.progress.categories[categoryId] = categoryStats;
  }
  saveProgress();
}

function advanceQuestion() {
  if (app.session.index < app.session.questions.length - 1) {
    app.session.index += 1;
    renderQuestion();
  } else {
    finishSession();
  }
}

function finishSession() {
  const session = app.session;
  const correctCount = session.answers.filter(answer => answer.correct).length;
  const percent = Math.round((correctCount / session.answers.length) * 100);
  const durationSeconds = Math.max(0, Math.round((Date.now() - session.startedAt) / 1000));
  const minutes = Math.floor(durationSeconds / 60);
  const seconds = durationSeconds % 60;

  elements.resultMode.textContent = modeLabels[session.mode];
  elements.resultScore.textContent = `${correctCount} / ${session.answers.length}`;
  elements.resultDuration.textContent = `${percent}% correct · ${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
  const weakCategories = new Map();
  session.answers.filter(answer => !answer.correct).forEach(({ question }) => {
    question.categories.forEach(categoryId => weakCategories.set(categoryId, (weakCategories.get(categoryId) ?? 0) + 1));
  });

  if (weakCategories.size > 0) {
    const recommendations = [...weakCategories.entries()]
      .sort((left, right) => right[1] - left[1])
      .slice(0, 3)
      .map(([categoryId]) => categoryLabel(categoryId));
    elements.resultRecommendation.textContent = `Recommended practice: ${recommendations.join(", ")}.`;
  } else {
    elements.resultRecommendation.textContent = "No missed categories this time. Great work!";
  }
  if (session.mode === "assessment") {
    elements.resultRecommendation.textContent += percent >= app.passPercent
      ? ` You met the ${app.passPercent}% assessment target.`
      : ` The assessment target is ${app.passPercent}%.`;
  }

  elements.answerReview.replaceChildren();
  for (const [index, answer] of session.answers.entries()) {
    elements.answerReview.append(createReviewItem(answer, index + 1));
  }
  showScreen("results");
}

function createReviewItem(answer, index) {
  const { question, selectedIndex, correct } = answer;
  const article = document.createElement("article");
  article.className = "review-item";
  const heading = document.createElement("div");
  heading.className = "review-item-heading";
  const result = document.createElement("span");
  result.className = `review-result ${correct ? "is-correct" : "is-incorrect"}`;
  result.textContent = correct ? "Correct" : "Review";
  const prompt = document.createElement("span");
  prompt.textContent = `${String(index).padStart(2, "0")} · ${question.prompt}`;
  heading.append(result, prompt);
  const answerLine = document.createElement("p");
  answerLine.className = "review-correct-answer";
  answerLine.textContent = correct
    ? `Your answer: ${question.options[selectedIndex]}`
    : `Your answer: ${question.options[selectedIndex]} · Correct: ${question.options[question.correct_index]}`;
  const explanation = document.createElement("p");
  explanation.textContent = question.explanation;
  article.append(heading, answerLine, explanation);
  return article;
}

async function initialize() {
  loadProgress();
  try {
    const [questionsResponse, manifestResponse, categoriesResponse] = await Promise.all([
      fetch("./content/questions.json"),
      fetch("./content/manifest.json"),
      fetch("./content/categories.json")
    ]);
    if (!questionsResponse.ok || !manifestResponse.ok || !categoriesResponse.ok) {
      throw new Error("The quiz content could not be loaded. Check that the site was deployed with the content folder.");
    }
    const [questionData, manifest, categoryData] = await Promise.all([
      questionsResponse.json(),
      manifestResponse.json(),
      categoriesResponse.json()
    ]);
    app.questions = (questionData.questions ?? []).filter(question => question.active);
    app.categories = categoryData.categories ?? [];
    app.passPercent = Number(manifest.assessment_pass_percent) || 80;
    if (app.questions.length === 0) throw new Error("The content pack does not contain any active questions.");
    updateHome();
    elements.startPractice.disabled = false;
    elements.startWeak.disabled = false;
    elements.startAssessment.disabled = false;
    if (app.storageAvailable) showStatus("");
  } catch (error) {
    showStatus(error instanceof Error ? error.message : "The quiz could not be initialized.", true);
    elements.questionCount.textContent = "Question set unavailable";
    elements.startPractice.disabled = true;
    elements.startWeak.disabled = true;
    elements.startAssessment.disabled = true;
  }
}

elements.startPractice.addEventListener("click", () => beginSession("practice"));
elements.startWeak.addEventListener("click", () => beginSession("weak"));
elements.startAssessment.addEventListener("click", () => beginSession("assessment"));
elements.answerForm.addEventListener("submit", submitAnswer);
elements.nextQuestion.addEventListener("click", advanceQuestion);
elements.quitQuiz.addEventListener("click", () => {
  app.session = null;
  showScreen("home");
});
elements.backHome.addEventListener("click", () => {
  app.session = null;
  showScreen("home");
});

function openImageDialog() {
  if (!elements.image.src) return;
  elements.largeImage.src = elements.image.src;
  elements.largeImage.alt = elements.image.alt;
  elements.imageDialog.showModal();
}

elements.zoomImage.addEventListener("click", openImageDialog);
elements.image.addEventListener("click", openImageDialog);
elements.closeImage.addEventListener("click", () => elements.imageDialog.close());
elements.imageDialog.addEventListener("click", event => {
  if (event.target === elements.imageDialog) elements.imageDialog.close();
});

initialize();
