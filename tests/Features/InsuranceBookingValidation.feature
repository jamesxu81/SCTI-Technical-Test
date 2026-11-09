@UI
Feature: Insurance Booking Journey - validation
  Form rules found on the live site. No scenario goes past the Traveller details step.

  Background:
    Given I navigate to the insurance portal

  Scenario: The quote form requires every field
    When I click Get plan options
    Then I should see the quote form error "All fields are required."
    And no quote should be requested

  Scenario Outline: Traveller ages outside the allowed range are rejected
    When I try to add "<traveller>" travellers
    Then I should see the traveller error "<message>"
    And the travellers selector should stay open

    # Children need an adult on the policy, otherwise a different rule is shown.
    Examples:
      | traveller                      | message                                          |
      | Adult:17                       | Adult traveller must be aged between 18 and 118  |
      | Adult:119                      | Adult traveller must be aged between 18 and 118  |
      | Adult:40, DependentChild:21    | Dependent traveller must be aged 20 or under     |
      | Adult:40, NonDependentChild:18 | Non-dependent traveller must be aged 17 or under |

  Scenario Outline: The return date must be at least 2 days after the departure date
    When I add travel dates "today+30" and "today+44"
    Then the travel dates should be "today+30" to "today+44"
    When I change the return date to "<return>"
    Then I should see the travel dates error "Return date must be at least 2 days after the Departure date"
    And the travel dates selector should stay open

    Examples:
      | return   |
      | today+20 |
      | today+30 |

  Scenario: Traveller details enforce eligibility, the declarations and a matching date of birth
    Given I have a quote for "Japan" from "today+30" to "today+44" for "Adult:40, DependentChild:10, NonDependentChild:12"
    When I select a plan and customise it
    And I choose discount "No", excess "$200", cancellation cover "$2,500", and extra cover "None"
    And I click Next to Traveller details
    And I continue without answering the eligibility questions or declarations
    Then I should see 3 "This field is required" messages
    And I should see 2 "You must agree to the declaration to continue." messages
    And I should still be on the traveller details step
    When I answer "No" to whether all travellers are New Zealand residents
    Then I should see the eligibility message "Sorry, you don't appear to meet our minimum eligibility criteria for this type of travel insurance."
    When I complete traveller eligibility and privacy declaration
    And I enter date of birth "age:30" for the main policyholder
    Then I should see the date of birth error "Entered age and date of birth do not match."
    And I should be offered "Re-enter" or "Update quote"
    When I choose "Re-enter" in the date of birth prompt
    Then the total premium should be unchanged

  Scenario: A traveller added on Traveller details can change type and be removed
    Given I have a quote for "Australia" from "today+30" to "today+44" for "Adult:40, DependentChild:10, NonDependentChild:12"
    When I select a plan and customise it
    And I choose discount "No", excess "$200", cancellation cover "$2,500", and extra cover "None"
    And I click Next to Traveller details
    And I complete traveller eligibility and privacy declaration
    And I add another traveller
    Then I should see a traveller form headed "Adult 2"
    When I change the new traveller's type to "Dependent child (20 and under)"
    Then I should see a traveller form headed "Dependent child 2"
    And the "Dependent child 2" form should only ask for first name, last name and date of birth
    When I remove the new traveller
    Then I should see 3 traveller forms
    And the total premium should be unchanged
