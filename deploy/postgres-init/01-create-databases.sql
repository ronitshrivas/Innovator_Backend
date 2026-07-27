-- Creates one database per microservice. Runs automatically the first time the
-- postgres container starts (empty data volume). Safe to leave as-is.
CREATE DATABASE innovator_auth;
CREATE DATABASE innovator_profile;
CREATE DATABASE innovator_feed;
CREATE DATABASE innovator_chat;
CREATE DATABASE innovator_search;
CREATE DATABASE innovator_ecommerce;
CREATE DATABASE innovator_elearning;
CREATE DATABASE innovator_events;
CREATE DATABASE innovator_research;
