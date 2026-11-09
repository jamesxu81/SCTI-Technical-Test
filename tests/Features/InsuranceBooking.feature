@UI
Feature: Insurance Booking Journey
  As a New Zealand resident travelling overseas
  I want to quote and customise a one return journey policy
  So that I can enter my travellers' details before buying

  Scenario Outline: Quote, customise and add 1 adult, 1 dependent and 1 non-dependent traveller
    Given I navigate to the insurance portal
    When I select "One return journey" journey
    And I add destination "<destinations>"
    And I add travel dates "<departure>" and "<return>"
    And I add "Adult:<adult age>, DependentChild:<dependent age>, NonDependentChild:<non-dependent age>" travellers
    And I click Get plan options
    Then I should see plan options available
    And the quote request should contain destinations "<destination codes>" and travellers "Adult:<adult age>, DependentChild:<dependent age>, NonDependentChild:<non-dependent age>"
    When I select a plan and customise it
    And I choose discount "<discount>", excess "<excess>", cancellation cover "<cancellation cover>", and extra cover "<extra cover>"
    Then the plan price should equal the base price plus the premium changes
    And the latest plan update request should contain discount "<discount>", excess "<excess>", cancellation cover "<cancellation cover>" and extra cover "<extra cover>"
    When I click Next to Traveller details
    Then I should see traveller eligibility section
    When I complete traveller eligibility and privacy declaration
    And I add adult traveller with firstname "Testadult", lastname "Example", and DOB "age:<adult age>"
    And I enter contact details for the main policyholder with email "test.adult@example.com", mobile "0210000000" and address "1 Queen Street, Auckland CBD, Auckland, New Zealand"
    And I add dependent traveller with firstname "Testdependent", lastname "Example", and DOB "age:<dependent age>"
    And I add non-dependent traveller with firstname "Testnondependent", lastname "Example", and DOB "age:<non-dependent age>"
    Then I should see all travellers added successfully

    Examples:
      | destinations     | destination codes | departure | return   | adult age | dependent age | non-dependent age | discount | excess | cancellation cover | extra cover            |
      | Japan, Australia | JPN, AUS          | today+30  | today+44 | 40        | 10            | 12                | Yes      | $100   | $5,000             | Skiing or snowboarding |
      | Australia        | AUS               | today+60  | today+70 | 65        | 20            | 17                | No       | $0     | Unlimited          | Cruise                 |
