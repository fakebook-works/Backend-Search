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
```

### Schema Explanation

*   **`search_entity` Table (The Result Framework):** This table contains the list of "things" that can be found. The crucial element here is the `sort_key` column[cite: 4]. When searching for "Nguyen", there might be 1 million people; the system looks at the `sort_key` and fetches those with high scores (e.g., verified accounts, 5000 friends) to display first. The `privacy_level` acts as a security barrier; results with `privacy_level = 0` (Only me) are instantly discarded if the searcher is not the `owner_id` (The ACL problem)[cite: 4].
*   **`inverted_index` Table (The Index):** This is how the "Relationship Network" is turned into "Keywords". Instead of saving full sentences, when User "Nguyen Van A" (ID = 10, lives in Hanoi, friend of User 4) is created, the backend system separates the data and saves it as multiple terms: `name:nguyen`, `name:van`, `name:a`, `lives_in:hanoi`, and `friend:4`.

### 3. How the Search Magic Works (Practical Example)

Imagine our "Fakebook" network currently has 3 users[cite: 4]:
*   **User ID = 1:** Nguyen Van A (Lives in Hanoi, friend of User 2).
*   **User ID = 2:** Tran Thi B (Lives in Hanoi, friend of User 1).
*   **User ID = 3:** Nguyen Van C (Lives in Da Nang, highly popular verified account).

#### Data Storage Simulation

**`search_entity` Table:**
*   ID 1 | USER | sort_key: 10 | Public | Owner: 1 (Nguyen Van A, low popularity).
*   ID 2 | USER | sort_key: 15 | Public | Owner: 2 (Tran Thi B).
*   ID 3 | USER | sort_key: 999 | Public | Owner: 3 (Nguyen Van C, verified account so sort_key is very high).

**`inverted_index` Table:**
*   `name:nguyen` -> ID 1, ID 3.
*   `lives_in:hanoi` -> ID 1, ID 2.
*   `friend:2` -> ID 1.

#### Query Execution

Now, a user types into the search bar: *"Find people named Nguyen living in Hanoi"*``.

If using a traditional Database (using `LIKE`), the server would have to scan the User table from the first row to the last to see who is named "Nguyen" AND who is in "Hanoi", causing massive fatigue for a million users.

However, with the Unicorn design, the database performs this magic extremely fast via `and` operators for set intersection:

*   **Step 1:** Find IDs with `term = name:nguyen`. The Database instantly fetches: `[ID 1, ID 3]`.
*   **Step 2:** Find IDs with `term = lives_in:hanoi`. The Database instantly fetches: `[ID 1, ID 2]`.
*   **Step 3 (Set Intersection):** The common ID appearing in both lists is ID 1 (Nguyen Van A).

**Conclusion:** The system returns User ID 1[cite: 4]. This process happens in just a few milliseconds because the Database retrieves data directly using the Primary Key, completely avoiding a full-scan!
