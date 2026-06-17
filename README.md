# Fakebook Search Infrastructure: Database Schema & Architecture

This repository contains the core architecture and database schema for the Fakebook Search Engine. 

## 1. Four Core Pillars of the Search System

Our search system operates on four foundational pillars inspired by Facebook's architecture:

* **Social Graph Search:** Unlike Google, which searches static web documents linked by hyperlinks, this system searches across a massive Social Graph. The network consists of Nodes (users, pages, posts) and Edges representing interactions (friends, likes, comments).
* **"Unicorn" Indexing Engine (Inverted Index):** Traditional graph databases cannot handle the massive data volume, so we utilize a Unicorn-inspired system. Instead of pure text indexing, it indexes relationship chains (e.g., searching for friends of user ID 4 uses the term `friend:4`) to extract a list of matching IDs. To achieve ultra-fast speeds, it does not scan millions of results but uses a `sort-key` to pre-evaluate static scores, extracting only the top most important results to the top.
* **Real-time Updates with "Wormhole":** A new post becomes searchable in seconds thanks to Wormhole technology. This distribution pipeline continuously listens to raw data from the MySQL database, packages new posts, and pushes them directly into the Index engine in just a few seconds.
* **AI and Privacy Protection:** 
    * *Semantic Retrieval (AI):* The Unicorn engine runs parallel to a machine learning model called SSR. SSR converts text into multi-dimensional mathematical vectors, allowing the system to understand queries like "Italian coffee drink" and return posts containing the word "cappuccino". 
    * *Privacy (ACL):* Every query must pass through an Access Control List (ACL). If a post matches keywords but the owner set it to "Only me," the system automatically discards that result instantly.

## 2. SQL Search Schema Database (BETA 1)

Below is the foundational SQL schema designed to support this architecture:

```sql
-- Create Schema for the Search feature
CREATE SCHEMA IF NOT EXISTS fb_search;
SET search_path TO fb_search;

-- TABLE 1: SEARCH ENTITY (Represents the results to be displayed)
CREATE TABLE search_entity (
    entity_id       BIGINT PRIMARY KEY, -- ID of the result (user, post, or page ID)
    entity_type     VARCHAR(50) NOT NULL, -- Type of result: 'USER', 'POST', 'GROUP'
    
    -- STATIC RANK: Display priority score 
    sort_key        INT DEFAULT 0, -- Celebrities or highly-liked posts get higher scores
    
    -- PRIVACY (ACL) 
    privacy_level   INT DEFAULT 2, -- 0: Only me, 1: Friends, 2: Public
    owner_id        BIGINT NOT NULL, -- Owner of the entity (for permissions check)
    
    created_at      TIMESTAMPTZ DEFAULT now()
);

-- TABLE 2: KEYWORD INDEX (Inverted Index - The heart of the Unicorn system)
CREATE TABLE inverted_index (
    term            VARCHAR(255) NOT NULL, -- Keyword (e.g., 'name:nguyen', 'lives_in:hanoi', 'friend:4')
    entity_id       BIGINT REFERENCES search_entity(entity_id) ON DELETE CASCADE,
    
    -- Composite primary key to prevent duplication
    PRIMARY KEY (term, entity_id)
);

-- Create Index to maximize search speed
CREATE INDEX idx_term_search ON inverted_index(term);
