export interface Translations {
  common: {
    appName: string;
    appNameFull: string;
    appEdition: string;
    languageEnglish: string;
    languageSpanish: string;
    notifications: string;
    profileMenu: string;
    switchToLightMode: string;
    switchToDarkMode: string;
    close: string;
    viewAll: string;
    comingSoon: string;
    expandSidebar: string;
    collapseSidebar: string;
  };
  landing: {
    topBar: {
      goToApp: string;
      continueOnboarding: string;
      signOut: string;
      signIn: string;
    };
    hero: {
      badge: string;
      titleLine1: string;
      titleLine2: string;
      subtitle: string;
      ctaPrimary: string;
      ctaSecondary: string;
      flow: {
        mind: string;
        memory: string;
        practice: string;
        recall: string;
        application: string;
        capability: string;
        mastery: string;
        value: string;
      };
    };
    problem: {
      title: string;
      informationEyebrow: string;
      informationTitle: string;
      informationItems: {
        searchEngines: string;
        aiTools: string;
        videos: string;
        courses: string;
        content: string;
      };
      capabilityEyebrow: string;
      capabilityTitle: string;
      capabilityItems: {
        memory: string;
        thinking: string;
        application: string;
        problemSolving: string;
        valueCreation: string;
      };
      footer: string;
    };
    memoryParadox: {
      title: string;
      subtitle: string;
      traditionalTitle: string;
      traditionalSteps: { question: string; answer: string; dependency: string };
      traditionalDescription: string;
      badge: string;
      humanOsTitle: string;
      humanOsSteps: {
        question: string;
        practice: string;
        recall: string;
        application: string;
        evidence: string;
        mastery: string;
      };
      humanOsDescription: string;
      terms: {
        retrievalPractice: string;
        memoryConsolidation: string;
        schemaFormation: string;
        capabilityDevelopment: string;
      };
    };
    graphLearning: {
      eyebrow: string;
      title: string;
      subtitle: string;
      traditionalTitle: string;
      linearSteps: { chapter1: string; chapter2: string; chapter3: string; chapter4: string; chapter5: string };
      traditionalDescription: string;
      badge: string;
      humanOsTitle: string;
      nodes: {
        foundations: string;
        coreConcept: string;
        relatedIdea: string;
        appliedSkill: string;
        advancedTopic: string;
        specialization: string;
        mastery: string;
      };
      legend: { mastered: string; inProgress: string; locked: string };
      description: string;
      footer: string;
    };
    capabilityGrowth: {
      title: string;
      subtitle: string;
      items: {
        criticalThinking: string;
        aiAutomation: string;
        communication: string;
        problemSolving: string;
        leadership: string;
      };
      footer: string;
    };
    growthLoop: {
      title: string;
      subtitle: string;
      steps: {
        discover: { label: string; description: string };
        practice: { label: string; description: string };
        recall: { label: string; description: string };
        apply: { label: string; description: string };
        evidence: { label: string; description: string };
        mastery: { label: string; description: string };
        value: { label: string; description: string };
      };
    };
    layers: {
      title: string;
      items: {
        foundation: { title: string; description: string };
        exploration: { title: string; description: string };
        mastery: { title: string; description: string };
        professional: { title: string; description: string };
        frontier: { title: string; description: string };
        creator: { title: string; description: string };
      };
    };
    forIndividuals: {
      eyebrow: string;
      title: string;
      description: string;
      benefits: {
        discoverStrengths: string;
        developExpertise: string;
        improveMemory: string;
        buildConfidence: string;
        adaptContinuously: string;
        createOpportunities: string;
        developPurpose: string;
      };
    };
    forOrganizations: {
      eyebrow: string;
      title: string;
      description: string;
      benefits: {
        capabilityIntelligence: string;
        workforceAdaptability: string;
        futureReadiness: string;
        humanCenteredAiAdoption: string;
        skillsVisibility: string;
        growthMeasurement: string;
      };
    };
    vision: {
      title: string;
      titleHighlight: string;
      description: string;
      notLabel: string;
      goalLabel: string;
      notConvenience: string;
      goalCapability: string;
      notInformation: string;
      goalTransformation: string;
    };
    cta: {
      title: string;
      description1: string;
      description2: string;
      button: string;
    };
    footer: {
      tagline: string;
      links: { vision: string; framework: string; capabilities: string; organizations: string; privacy: string; terms: string };
      copyright: string;
    };
  };
  nav: {
    today: string;
    growthPlan: string;
    comingSoon: string;
    programs: string;
    myPrograms: string;
    capabilities: string;
    myCapabilities: string;
    goals: string;
    evidence: string;
    myEvolution: string;
    myBrain: string;
    agents: string;
    organizationContext: string;
    profile: string;
    preferences: string;
    privacyAndData: string;
    help: string;
    signOut: string;
    settings: string;
    more: string;
    mobileNavigation: string;
    groupToday: string;
    groupGrowth: string;
    groupSupport: string;
    groupCreate: string;
    createCourse: string;
  };
  privacy: {
    privateToYou: string;
    sharedWithOrganization: string;
    requiredForRole: string;
  };
  privacyPage: {
    heroTitle: string;
    heroSubtitle: string;
    commitments: {
      noSharing: string;
      noSelling: string;
      noAiTraining: string;
      azureSql: string;
    };
    openModalButton: string;
    modal: {
      title: string;
      intro: string;
      sections: {
        noSharing: { title: string; body: string };
        noAiTraining: { title: string; body: string };
        azureSql: { title: string; body: string; bullets: string[] };
      };
      closingNote: string;
      closeButton: string;
    };
  };
  greeting: {
    morning: string;
    afternoon: string;
    evening: string;
  };
  todayPage: {
    loadingTitle: string;
    loadingDescription: string;
  };
  progressOverview: {
    sectionTitle: string;
    programsEnrolled: string;
    capabilitiesEnrolled: string;
    overallProgress: string;
    nodesMastered: string;
    averageScore: string;
    noPrograms: string;
    viewPrograms: string;
  };
  alignment: {
    sectionTitle: string;
    becoming: string;
    drivenBy: string;
    personalGoal: string;
    organizationAlignment: string;
    sharedCapabilities: string;
    seeAllGoals: string;
    helpsBothMessage: string;
    helpsYouMessage: string;
  };
  capabilities: {
    sectionTitle: string;
    supports: string;
    standalone: string;
    levelValue: string;
    levels: {
      none: string;
      basico: string;
      intermedio: string;
      avanzado: string;
    };
  };
  continueLearning: {
    sectionTitle: string;
    resume: string;
    emptyTitle: string;
    emptyHint: string;
    emptyCta: string;
  };
  brain: {
    pageTitle: string;
    subtitle: string;
    totalNeurons: string;
    totalAreas: string;
    searchPlaceholder: string;
    emptyTitle: string;
    emptyHint: string;
    emptyCta: string;
    selectNodeHint: string;
    enterAreaHint: string;
    backToAllAreas: string;
    askAllComingSoon: string;
    panelAttempts: string;
    panelScore: string;
    panelFirstCompleted: string;
    panelLastCompleted: string;
    talkToAgent: string;
    redoFiveSteps: string;
    fadingMessage: string;
    deepDiveCardsTitle: string;
    randomCard: string;
    explainCardCta: string;
    automaticBadge: string;
    automaticBadgeHint: string;
    explainerTitle: string;
    explainerToggleShow: string;
    explainerToggleHide: string;
    explainerLegendTitle: string;
    explainerLegendRing: string;
    explainerColorStrong: string;
    explainerColorFading: string;
    explainerColorForgotten: string;
    explainerLegendPercent: string;
    explainerLegendAutomatic: string;
    explainerAlgorithmTitle: string;
    explainerAlgorithmBody: string;
    explainerBenefitsTitle: string;
    explainerBenefitReal: string;
    explainerBenefitIQ: string;
    explainerBenefitSocial: string;
    loadingSummary: string;
    closePanel: string;
    totalNeedsReview: string;
    viewGrid: string;
    viewGraph: string;
    noSearchResults: string;
    filterBySubject: string;
    allSubjects: string;
    filterByCourse: string;
    allCourses: string;
    filterByCategory: string;
    categoryJobSkills: string;
    categorySoftSkills: string;
    categoryDepartmentProcesses: string;
    categoryHrProcesses: string;
    filterByStatus: string;
    statusMastered: string;
    statusNeedsReview: string;
    filterByLastReviewed: string;
    dateFromLabel: string;
    dateToLabel: string;
    clearDates: string;
    loadingTitle: string;
    loadingDescription: string;
    memoryScoreLabel: string;
    memoryScoreCaption: string;
  };
  growth: {
    subtitle: string;
    loading: string;
    emptyHint: string;
    countLabel: string;
    todayDelta: string;
    explainer: string;
  };
  humanState: {
    sectionTitle: string;
    focus: string;
    energy: string;
    purpose: string;
    confidence: string;
    checkInPrompt: string;
    checkInHint: string;
    checkInSubmit: string;
    checkInSaving: string;
    checkedInToday: string;
    updateCheckIn: string;
    streak: string;
  };
  growthPlan: {
    pageTitle: string;
    overview: {
      sectionTitle: string;
      description: string;
      stepOf: string;
      statusCompleted: string;
      statusInProgress: string;
      statusNotStarted: string;
      statusLocked: string;
      continueAction: string;
      startAction: string;
      lockedHint: string;
      backToGrowthPlan: string;
      steps: {
        currentSituation: { title: string; description: string; items: string[] };
        futureDirection: { title: string; description: string; items: string[] };
        workAndExperience: { title: string; items: string[] };
        yourFuture: { title: string; items: string[] };
        growthContext: { title: string; items: string[] };
        startingPoint: { title: string; description: string; items: string[] };
        agentProposedPlan: { title: string; items: string[] };
        humanReview: { title: string; items: string[] };
        planActivation: { title: string; items: string[] };
        continuousEvolution: { title: string; items: string[] };
      };
      result: {
        heading: string;
        planName: string;
        flow: string[];
      };
    };
    currentSituation: {
      pageTitle: string;
      stepLabel: string;
      headline: string;
      description: string;
      areasHeading: string;
      areasHint: string;
      levelHeading: string;
      levelHint: string;
      levels: {
        beginner: string;
        intermediate: string;
        advanced: string;
      };
      levelDescriptions: {
        beginner: string;
        intermediate: string;
        advanced: string;
      };
      selectAreasEmptyHint: string;
      continueAction: string;
    };
    futureDirection: {
      pageTitle: string;
      stepLabel: string;
      headline: string;
      description: string;
      goalsHeading: string;
      goalsHint: string;
      goals: {
        helpFamily: string;
        extraIncome: string;
        careerChange: string;
        dailyIndependence: string;
        loveOfLearning: string;
        personalChallenge: string;
        wellbeing: string;
        ownProject: string;
        reclaimFocus: string;
      };
      goalDescriptions: {
        helpFamily: string;
        extraIncome: string;
        careerChange: string;
        dailyIndependence: string;
        loveOfLearning: string;
        personalChallenge: string;
        wellbeing: string;
        ownProject: string;
        reclaimFocus: string;
      };
      motivationsHeading: string;
      motivationsHint: string;
      motivations: {
        growth: string;
        helpingOthers: string;
        independence: string;
        curiosity: string;
        security: string;
        creativity: string;
        pride: string;
      };
      selectGoalsEmptyHint: string;
      continueAction: string;
      savingAction: string;
      saveError: string;
    };
    startingPoint: {
      pageTitle: string;
      stepLabel: string;
      headline: string;
      description: string;
      emptySubject: string;
      levels: {
        beginner: string;
        intermediate: string;
        advanced: string;
      };
      gapHint: string;
      gapPlaceholder: string;
      gapAddAction: string;
      gapRemoveAction: string;
      noSubjectsHint: string;
      continueAction: string;
      savingAction: string;
      saveError: string;
      goalPlanner: {
        heading: string;
        description: string;
        placeholder: string;
        submitAction: string;
        loading: string;
        noMatch: string;
        recommendedProgram: string;
        recommendedCapabilities: string;
        contextIntro: string;
        levelQuestion: string;
        levelNone: string;
        entryPointHint: string;
        acceptProgramAction: string;
        chooseIndividualAction: string;
        addedConfirmation: string;
      };
    };
    workContext: {
      pageTitle: string;
      stepLabel: string;
      headline: string;
      description: string;
      fields: {
        organization: string;
        businessUnit: string;
        department: string;
        team: string;
        currentRole: string;
        roleLevel: string;
        jobFamily: string;
        workLocation: string;
        employmentType: string;
        preferredLanguage: string;
        manager: string;
      };
      employmentTypes: {
        fullTime: string;
        partTime: string;
        contractor: string;
        intern: string;
      };
      source: {
        providedByOrganization: string;
        syncedFromProfile: string;
        lastSynchronized: string;
        verified: string;
      };
      privacyNotice: string;
      actions: {
        confirmCorrect: string;
        requestCorrection: string;
        continueToRoleRequirements: string;
      };
      confirmedMessage: string;
      confirmedStatusLabel: string;
      correction: {
        dialogTitle: string;
        dialogDescription: string;
        reasonLabel: string;
        reasons: {
          organization: string;
          department: string;
          team: string;
          role: string;
          roleLevel: string;
          manager: string;
          workLocation: string;
          other: string;
        };
        detailsLabel: string;
        submit: string;
        cancel: string;
        submittedMessage: string;
        pendingStatusLabel: string;
      };
      incomplete: {
        heading: string;
        message: string;
        action: string;
      };
      error: {
        heading: string;
        message: string;
        retry: string;
        configureRole: string;
      };
      configureRole: {
        uploadPdf: string;
        pasteDescription: string;
        describeWithGuide: string;
        pasteHeading: string;
        describeHeading: string;
        textareaPlaceholder: string;
        save: string;
        cancel: string;
        uploading: string;
        uploaded: string;
        uploadError: string;
      };
    };
    roleExperience: {
      pageTitle: string;
      headline: string;
      description: string;
      jobDescriptionSource: {
        pageTitle: string;
        introduction: string;
        sourceTypes: {
          organizationProvided: string;
          employeeProvided: string;
          pendingOrganizationReview: string;
        };
        official: {
          jobTitleLabel: string;
          rolePurposeLabel: string;
          roleSummaryLabel: string;
          primaryResponsibilitiesLabel: string;
          expectedOutcomesLabel: string;
          requiredExperienceLabel: string;
          organizationOwnerLabel: string;
          versionLabel: string;
          lastUpdatedLabel: string;
          verificationStatusLabel: string;
          viewFullDescription: string;
          collapse: string;
          reflectsMyRole: string;
          workIsDifferent: string;
          addMissingContext: string;
          requestOrganizationReview: string;
        };
        empty: {
          heading: string;
          message: string;
          uploadAction: string;
          pasteAction: string;
          describeAction: string;
          requestFromOrganizationAction: string;
        };
        employeeContext: {
          label: string;
          pasteHeading: string;
          describeHeading: string;
          rolePurposeFieldLabel: string;
          mainResponsibilitiesFieldLabel: string;
          expectedResultsFieldLabel: string;
          addItem: string;
          save: string;
          uploadedFileNotice: string;
        };
        missingContextForm: {
          fieldLabel: string;
          save: string;
        };
        organizationReviewRequested: string;
        privacyNotice: string;
        confirm: {
          action: string;
          successMessage: string;
        };
        guideTip: {
          heading: string;
          body: string;
        };
      };
      jobDescription: {
        sectionTitle: string;
        rolePurposeLabel: string;
        primaryResponsibilitiesLabel: string;
        expectedOutcomesLabel: string;
        organizationRequirementsLabel: string;
        viewFullDescription: string;
        reflectsMyRole: string;
        workIsDifferent: string;
        addMissingResponsibility: string;
      };
      missingJobDescription: {
        heading: string;
        message: string;
        startDraft: string;
        describeWhatYouDoLabel: string;
        saveDraft: string;
        employeeProvidedLabel: string;
      };
      professionalProfile: {
        sectionTitle: string;
        useExistingProfile: string;
        uploadResume: string;
        addManually: string;
        buildWithAgent: string;
        skipForNow: string;
      };
      resumeUpload: {
        dropzoneLabel: string;
        dropzoneHint: string;
        selectFile: string;
        uploading: string;
        processing: string;
        processingDescription: string;
        extractedHeading: string;
        extractedDescription: string;
        error: string;
        retry: string;
        removeFile: string;
      };
      sources: {
        organization: string;
        jobDescription: string;
        resume: string;
        employeeDeclared: string;
        agentInferred: string;
      };
      validationStatuses: {
        unvalidated: string;
        needsValidation: string;
        partiallyValidated: string;
        validated: string;
      };
      visibility: {
        privateToYou: string;
        sharedWithOrganization: string;
        organizationProvided: string;
        usedForGrowthRecommendations: string;
      };
      alignmentGuide: {
        title: string;
        summary: {
          introLine1: string;
          introLine2: string;
          dimensionsHeading: string;
          sourcesHeading: string;
          sourcesDescription: string;
          provisionalNotice: string;
          startReview: string;
          reviewSources: string;
          hideSources: string;
        };
        stepLabels: {
          summary: string;
          expectedOutcomes: string;
          capabilities: string;
          requiredKnowledge: string;
          professionalMethods: string;
          organizationalProcedures: string;
          governance: string;
          tools: string;
          evidenceExpectations: string;
          performanceCriteria: string;
          businessValue: string;
          finalReview: string;
        };
        placeholder: {
          heading: string;
          message: string;
          backToSummary: string;
        };
      };
    };
    direction: {
      sectionTitle: string;
      stepOf: string;
      futureSelfQuestion: string;
      goalQuestion: string;
      motivationQuestion: string;
      defineMyOwn: string;
      back: string;
      continue: string;
      saveAndContinueLater: string;
      summaryTitle: string;
      edit: string;
    };
    roleRequirements: {
      sectionTitle: string;
      roleLabel: string;
      coreCategory: string;
      policyCategory: string;
      futureReadyCategory: string;
      requiredLevel: string;
      currentLevel: string;
      evidenceVerified: string;
      evidenceNotYetDemonstrated: string;
      viewAll: string;
    };
    orgPriorities: {
      sectionTitle: string;
      whyOrg: string;
      whyYou: string;
      capabilitiesRequired: string;
    };
    alignment: {
      sectionTitle: string;
      yourFuture: string;
      yourRole: string;
      organization: string;
      sharedCapabilities: string;
      completeDirectionPrompt: string;
    };
    paths: {
      sectionTitle: string;
      requiredGroup: string;
      recommendedGroup: string;
      personalGroup: string;
      why: string;
      develops: string;
      demonstration: string;
      weeklyRhythm: string;
      weeklyRhythmValue: string;
      capabilitiesCount: string;
      projectsCount: string;
      recallCyclesCount: string;
      evidenceCount: string;
      targetIndependence: string;
      languageAvailable: string;
      dueDate: string;
      start: string;
      explore: string;
    };
    builder: {
      sectionTitle: string;
      primaryLabel: string;
      requiredLabel: string;
      personalLabel: string;
      choosePrimary: string;
      chooseRequired: string;
      choosePersonal: string;
      overloadWarning: string;
      firstActionsTitle: string;
      firstActions: {
        reflection: string;
        baselineRecall: string;
        chooseProject: string;
        reviewPolicy: string;
        defineEvidence: string;
      };
      buildCta: string;
      reviewCta: string;
    };
    activePlan: {
      sectionTitle: string;
      primary: string;
      required: string;
      personal: string;
      readiness: string;
      nextAction: string;
      dueDate: string;
      viewFullPlan: string;
    };
  };
  onboarding: {
    eyebrow: string;
    titleIndividual: string;
    titleOrganization: string;
    subtitleIndividual: string;
    subtitleOrganization: string;
    modeIndividual: string;
    modeOrganization: string;
    yourInfo: string;
    firstName: string;
    lastName: string;
    emailLabel: string;
    yourCompany: string;
    companyName: string;
    companyAddress: string;
    companyEmail: string;
    companyPhone: string;
    languageLabel: string;
    errorMessage: string;
    creating: string;
    createAccount: string;
    createOrganization: string;
  };
  profile: {
    loadingTitle: string;
    loadingDescription: string;
    personalInfoTitle: string;
    savedLabel: string;
    changePhoto: string;
    firstName: string;
    lastName: string;
    displayName: string;
    phone: string;
    preferredLanguage: string;
    country: string;
    countryPlaceholder: string;
    timeZone: string;
    timeZonePlaceholder: string;
    dateOfBirth: string;
    occupation: string;
    occupationPlaceholder: string;
    company: string;
    companyPlaceholder: string;
    companyReadOnlyHint: string;
    biography: string;
    biographyPlaceholder: string;
    loadError: string;
    saveError: string;
    photoError: string;
    saving: string;
    saveChanges: string;
  };
}
