# Test-Driven Development & Quality Engineering

## Core Technologies & Competencies

- **Test-Driven Development (TDD)**: Applying the red-green-refactor cycle to design modular, testable code from the outset. Focus on scoping precise behavioral requirements before writing implementation logic.
- **Ephemeral Execution Environments**: Executing test suites in isolated, state-free environments (e.g., using **Testcontainers** or dynamic Docker agents) locally or dynamically within CI/CD pipelines to ensure absolute reproducibility.
- **White Box vs. Black Box Testing**: 
  - *White Box*: Deep internal logic testing with full knowledge of the source code (typically unit and integration levels).
  - *Black Box*: Behavior-driven testing from an external user or consumer perspective without knowledge of internal structures.

## Testing Methodologies & Execution

- **Unit Testing**: Validating individual functions or components in absolute isolation, mocking external dependencies to ensure fast, deterministic feedback.
- **Integration Testing**: Verifying the interactions between connected components, APIs, and databases (e.g., using ephemeral databases via Testcontainers).
- **System Testing**: Evaluating the complete, fully integrated application environment to verify it meets holistic architectural and business requirements.
- **Acceptance Testing**: End-to-end business flow validation against user stories and requirements, ensuring the software solves the actual business problem.
- **Regression Testing**: Near-100% automated suites executed on every CI/CD pipeline run to guarantee new code integrations do not break existing functionality.
- **Smoke & Sanity Testing**: 
  - *Smoke Testing*: Fast, shallow execution on fresh ephemeral builds to verify critical infrastructure and core paths are alive.
  - *Sanity Testing*: Narrow, deep testing focused on newly added features or bug fixes to verify specific operational changes before broader testing.
- **Performance Testing**: Scoping and executing automated load, stress, and endurance tests (using tools like JMeter or K6) within staging environments to validate SLAs and system limits.
- **Security Testing**: Embedding SAST/DAST automatically into pipelines to check for vulnerabilities dynamically during the build and deployment phases.
- **Accessibility Testing (a11y)**: Automated UI checks (e.g., using axe-core) verifying WCAG compliance and ensuring the application is usable by everyone.

## Think Extra
- **Edge Cases**: Give up to 5 more obscure edge cases on applicable test suites or assertions. Define happy and unhappy path to start. Define and state limits.

## Document
- **Document Output**: On successful or failed test run, use compatible upload-artifact action to store results. 