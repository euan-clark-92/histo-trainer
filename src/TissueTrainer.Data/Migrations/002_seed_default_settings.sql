-- Migration: 002_seed_default_settings

INSERT OR IGNORE INTO app_settings (key, value) VALUES
    ('admin_password_hash', ''),
    ('content_path', 'content'),
    ('training_session_size', '15'),
    ('weak_session_size', '5'),
    ('assessment_session_size', '20'),
    ('assessment_pass_percent', '80'),
    ('mastery_weak_threshold', '0.75'),
    ('mastery_mastered_threshold', '0.90'),
    ('mastery_min_attempts', '10');

INSERT OR IGNORE INTO schema_migrations (version, name)
VALUES (2, '002_seed_default_settings');
